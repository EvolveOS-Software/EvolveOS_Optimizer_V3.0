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
        private static readonly EnumerationOptions SafeEnumOptions = new()
        {
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false
        };

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

                        long size = GetFileSizeFast(file);
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

            bool matchAll = patterns.Contains("*");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(root, "*", SafeEnumOptions); }
            catch { files = Array.Empty<string>(); }

            foreach (var f in files)
            {
                if (token.IsCancellationRequested) yield break;

                if (matchAll)
                {
                    if (seen.Add(f)) yield return f;
                    continue;
                }

                var fileName = Path.GetFileName(f);
                foreach (var p in patterns)
                {
                    if (FileSystemName.MatchesSimpleExpression(p, fileName, ignoreCase: true))
                    {
                        if (seen.Add(f)) yield return f;
                        break;
                    }
                }
            }

            if (!recurse) yield break;

            IEnumerable<string> dirs;
            try
            {
                dirs = Directory.EnumerateDirectories(root, "*", SafeEnumOptions)
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
            int uiThrottleCounter = 0;

            int safeIoThreads = Math.Clamp(Environment.ProcessorCount, 2, 8);

            Parallel.ForEach(result.FilesToDelete, new ParallelOptions
            {
                MaxDegreeOfParallelism = safeIoThreads,
                CancellationToken = token
            }, file =>
            {
                long size = 0;
                try
                {
                    size = new FileInfo(file).Length;
                    File.Delete(file);

                    Interlocked.Increment(ref deletedCount);
                    Interlocked.Add(ref deletedBytes, size);

                    if (Interlocked.Increment(ref uiThrottleCounter) % 25 == 0)
                    {
                        progress?.Report($"Deleted: {file}");
                    }
                }
                catch
                {
                    try
                    {
                        var lockers = UnlockHandleHelper.GetLockingProcessNames(file);

                        bool isCriticalSystemProcess = lockers.Any(locker =>
                            CriticalSystemProcesses.Any(sysProc =>
                                locker.Contains(sysProc, StringComparison.OrdinalIgnoreCase)));

                        if (isCriticalSystemProcess)
                        {
                            progress?.Report($"Skipped (System Lock): {Path.GetFileName(file)} is held by OS");
                        }
                        else
                        {
                            var fi = new FileInfo(file);
                            if (fi.Exists)
                            {
                                if (fi.IsReadOnly) fi.IsReadOnly = false;

                                UnlockHandleHelper.UnlockDirectory(file);

                                fi.Delete();

                                Interlocked.Increment(ref deletedCount);
                                Interlocked.Add(ref deletedBytes, size);
                                progress?.Report($"Force Deleted: {file}");
                            }
                        }
                    }
                    catch
                    {
                        progress?.Report($"Skipped (Access Denied): {Path.GetFileName(file)}");
                    }
                }
            });

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

            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
                                   .OrderByDescending(d => d.Length)
                                   .ToArray();
            }
            catch { return; }

            foreach (var sub in subDirs)
            {
                token.ThrowIfCancellationRequested();

                if (IsProtected(sub)) continue;

                TryDeleteSingleDirectory(sub, progress);
            }

            TryDeleteSingleDirectory(path, progress);
        }

        private static void TryDeleteSingleDirectory(string dirPath, IProgress<string>? progress)
        {
            try
            {
                var di = new DirectoryInfo(dirPath);

                if ((di.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    di.Attributes &= ~FileAttributes.ReadOnly;
                }

                if (di.GetFileSystemInfos().Length == 0)
                {
                    di.Delete();
                }
            }
            catch
            {
                try
                {
                    var lockers = UnlockHandleHelper.GetLockingProcessNames(dirPath);

                    bool isCriticalSystemProcess = lockers.Any(locker =>
                        CriticalSystemProcesses.Any(sysProc =>
                            locker.Contains(sysProc, StringComparison.OrdinalIgnoreCase)));

                    if (isCriticalSystemProcess)
                    {
                        progress?.Report($"Skipped (System Lock): Directory '{Path.GetFileName(dirPath)}' is held by OS");
                    }
                    else
                    {
                        UnlockHandleHelper.UnlockDirectory(dirPath);

                        var diRetry = new DirectoryInfo(dirPath);
                        if ((diRetry.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                            diRetry.Attributes &= ~FileAttributes.ReadOnly;

                        if (diRetry.GetFileSystemInfos().Length == 0)
                        {
                            diRetry.Delete();
                        }
                    }
                }
                catch
                {
                    progress?.Report($"Skipped (Access Denied): Directory '{Path.GetFileName(dirPath)}'");
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

        private static long GetFileSizeFast(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                return fi.Exists ? fi.Length : -1;
            }
            catch { return -1; }
        }

        private static bool IsExcluded(string path, List<ExclusionRule> rules)
        {
            foreach (var rule in rules)
                if (rule.Matches(path))
                    return true;
            return false;
        }

        private static readonly string[] CriticalSystemProcesses =
        {
            "svchost", "system", "services", "lsass", "csrss", "winlogon",
            "smss", "spoolsv", "explorer", "searchindexer", "wmiprvse",
            "trustedinstaller", "tiworker", "msmpeng", "nissrv",
            "wudfhost", "taskhostw", "sihost", "fontdrvhost", "dashost",
            "audiodg", "mousocoreworker", "securityhealthservice", "dllhost",
            "conhost", "runtimebroker", "searchhost", "startmenuexperiencehost"
        };

        private static readonly string[] ProtectedSegments =
        {
            @"\IndexedDB\chrome-extension_",
            @"\WinUI3",
            @"\EvolveOS",
            @".WebView2"
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