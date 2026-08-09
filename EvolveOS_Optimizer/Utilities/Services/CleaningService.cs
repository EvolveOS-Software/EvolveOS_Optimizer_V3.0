// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.IO.Enumeration;
using System.Threading;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public class CleaningService
    {
        #region Public API
        public Task<ScanResult> AnalyzeAsync(CleanerEntry entry, IProgress<string>? progress = null, CancellationToken token = default) =>
            Task.Run(() => Analyze(entry, progress, token), token);

        public Task<(int count, long bytes)> CleanAsync(ScanResult result, IProgress<string>? progress = null, CancellationToken token = default) =>
            Task.Run(() => Clean(result, progress, token), token);
        #endregion

        #region Analyze Logic
        private ScanResult Analyze(CleanerEntry entry, IProgress<string>? progress, CancellationToken token)
        {
            var result = new ScanResult { Entry = entry };
            var excluded = BuildExclusions(entry);

            IProgress<string>? entryProgress = progress is null ? null
                : new PrefixedProgress(entry.Name, progress);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var fileKey in entry.FileKeys)
            {
                try
                {
                    foreach (var file in FindFiles(fileKey, excluded, entryProgress, token))
                    {
                        if (token.IsCancellationRequested) break;

                        if (!seen.Add(file)) continue;

                        var size = TryGetDeletableSize(file);
                        if (size < 0) continue;

                        result.FilesToDelete.Add(file);
                        result.TotalBytes += size;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }

            foreach (var regKey in entry.RegKeys)
            {
                try
                {
                    result.RegistryToDelete.AddRange(RegistryHelp.FindRegistryItems(regKey));
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }

            return result;
        }

        private IEnumerable<string> FindFiles(FileKeyEntry fileKey, List<ExclusionRule> excluded, IProgress<string>? progress, CancellationToken token)
        {
            bool recurse = fileKey.Flag is FileKeyFlag.Recurse or FileKeyFlag.RemoveSelf;

            var patterns = fileKey.Pattern.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var dir in PathLocator.ResolvePaths(fileKey.Path))
            {
                if (token.IsCancellationRequested) yield break;
                if (!Directory.Exists(dir)) continue;

                foreach (var f in EnumerateFilesSafe(dir, patterns, excluded, recurse, progress, token))
                {
                    if (token.IsCancellationRequested) yield break;
                    if (!IsExcluded(f, excluded) && !IsProtected(f))
                        yield return f;
                }
            }
        }

        private static IEnumerable<string> EnumerateFilesSafe(string root, string[] patterns, List<ExclusionRule> excluded, bool recurse, IProgress<string>? progress = null, CancellationToken token = default)
        {
            var scanRoot = root.TrimEnd('\\') + "\\";
            if (excluded.Any(rule => rule.Pattern is null && scanRoot.StartsWith(rule.DirPrefix, StringComparison.OrdinalIgnoreCase)))
                yield break;

            progress?.Report(root);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in patterns)
            {
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(root, p); }
                catch { files = []; }
                foreach (var f in files)
                {
                    if (token.IsCancellationRequested) yield break;
                    if (seen.Add(f)) yield return f;
                }
            }

            if (!recurse) yield break;

            IEnumerable<string> dirs;
            try
            {
                dirs = Directory.EnumerateDirectories(root)
                                .Where(d => (File.GetAttributes(d) & FileAttributes.ReparsePoint) == 0);
            }
            catch { yield break; }

            foreach (var sub in dirs)
            {
                token.ThrowIfCancellationRequested();
                foreach (var f in EnumerateFilesSafe(sub, patterns, excluded, recurse: true, progress, token))
                    yield return f;
            }
        }
        #endregion

        #region Clean Logic
        private (int count, long bytes) Clean(ScanResult result, IProgress<string>? progress, CancellationToken token)
        {
            int deletedCount = 0;
            long deletedBytes = 0;

            foreach (var file in result.FilesToDelete)
            {
                token.ThrowIfCancellationRequested();
                long size = 0;
                try
                {
                    size = new FileInfo(file).Length;
                    File.Delete(file);
                    deletedCount++;
                    deletedBytes += size;
                    progress?.Report($"Deleted: {file}");
                }
                catch
                {
                    try
                    {
                        UnlockHandleHelper.UnlockDirectory(file);
                        File.Delete(file);
                        deletedCount++;
                        deletedBytes += size;
                        progress?.Report($"Deleted: {file}");
                    }
                    catch
                    {
                        var lockers = UnlockHandleHelper.GetLockingProcessNames(file);
                        if (lockers.Count > 0)
                        {
                            string conflictList = string.Join(", ", lockers);
                            progress?.Report($"Skipped (In Use): {Path.GetFileName(file)} is locked by {conflictList}");
                        }
                        else
                        {
                            progress?.Report($"Error: Could not delete {Path.GetFileName(file)} (Access Denied)");
                        }
                    }
                }
            }

            foreach (var regItem in result.RegistryToDelete)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    RegistryHelp.DeleteRegistryItem(regItem);
                    deletedCount++;
                    progress?.Report($"Registry: {regItem}");
                }
                catch { }
            }

            foreach (var fk in result.Entry.FileKeys.Where(fk => fk.Flag == FileKeyFlag.RemoveSelf))
            {
                token.ThrowIfCancellationRequested();
                foreach (var resolved in PathLocator.ResolvePaths(fk.Path))
                {
                    TryPruneEmptyDirs(resolved, progress, token);
                }
            }

            return (deletedCount, deletedBytes);
        }

        private static void TryPruneEmptyDirs(string path, IProgress<string>? progress, CancellationToken token)
        {
            if (!Directory.Exists(path) || IsProtected(path)) return;

            try
            {
                foreach (var sub in Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
                                             .OrderByDescending(d => d.Length))
                {
                    token.ThrowIfCancellationRequested();

                    if (IsProtected(sub)) continue;

                    if (Directory.GetFileSystemEntries(sub).Length == 0)
                        Directory.Delete(sub);
                }

                if (Directory.GetFileSystemEntries(path).Length == 0)
                    Directory.Delete(path);
            }
            catch
            {
                try
                {
                    UnlockHandleHelper.UnlockDirectory(path);
                    if (Directory.GetFileSystemEntries(path).Length == 0)
                        Directory.Delete(path);
                }
                catch
                {
                    var lockers = UnlockHandleHelper.GetLockingProcessNames(path);
                    if (lockers.Count > 0)
                    {
                        progress?.Report($"Directory locked by: {string.Join(", ", lockers)}");
                    }
                }
            }
        }
        #endregion

        #region Helpers
        private List<ExclusionRule> BuildExclusions(CleanerEntry entry)
        {
            var rules = new List<ExclusionRule>();
            foreach (var ex in entry.ExcludeKeys)
            {
                if (ex.Type is ExcludeType.Reg) continue;
                foreach (var p in PathLocator.ResolvePaths(ex.Path))
                {
                    rules.Add(new ExclusionRule(p.TrimEnd('\\') + "\\", ex.Pattern));
                }
            }

            try
            {
                string baseDir = AppContext.BaseDirectory.TrimEnd('\\') + "\\";
                rules.Add(new ExclusionRule(baseDir, null));

                string tempPath = Path.GetTempPath();
                if (!tempPath.EndsWith("\\")) tempPath += "\\";

                string procName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                rules.Add(new ExclusionRule(tempPath, $"{procName}*"));
            }
            catch { }

            return rules;
        }

        private static long TryGetDeletableSize(string path)
        {
            const uint DELETE = 0x00010000;
            const uint FILE_SHARE_ALL = 0x7;
            const uint OPEN_EXISTING = 3;

            using var handle = Win32Helper.CreateFileW(path, DELETE, FILE_SHARE_ALL,
                                                       IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle.IsInvalid) return -1;

            try { return new FileInfo(path).Length; }
            catch { return -1; }
        }

        private static bool IsExcluded(string path, List<ExclusionRule> rules)
        {
            foreach (var rule in rules)
                if (rule.Matches(path))
                    return true;
            return false;
        }

        private static readonly string[] ProtectedSegments =
        {
            @"\IndexedDB\chrome-extension_", // Protects 1Password/Bitwarden etc.
            @"\WinUI3",                      // Prevents crashing the WinUI 3 renderer
            @"\EvolveOS",                    // Protects app-specific subfolders
            @".WebView2"                     // Prevents crashing embedded WebViews
        };

        private static bool IsProtected(string path) =>
            ProtectedSegments.Any(s => path.Contains(s, StringComparison.OrdinalIgnoreCase));

        private readonly record struct ExclusionRule(string DirPrefix, string? Pattern)
        {
            public bool Matches(string filePath)
            {
                if (!filePath.StartsWith(DirPrefix, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (Pattern is null) return true;

                if (Pattern.Contains('*') || Pattern.Contains('?'))
                {
                    var fileName = Path.GetFileName(filePath);
                    return FileSystemName.MatchesSimpleExpression(Pattern, fileName, ignoreCase: true);
                }

                var relativePath = filePath[DirPrefix.Length..];
                return relativePath.Equals(Pattern, StringComparison.OrdinalIgnoreCase);
            }
        }

        private sealed class PrefixedProgress(string prefix, IProgress<string> inner) : IProgress<string>
        {
            public void Report(string path) => inner.Report($"{prefix}  ›  {path}");
        }
        #endregion
    }
}