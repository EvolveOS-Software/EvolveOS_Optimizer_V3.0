// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.Maintenance
{
    #region Core Interfaces & Base Class

    public interface IDevCleanupRule
    {
        string Name { get; }
        string Category { get; }
        string Description { get; }
        bool IsDefaultSelected { get; }

        TimeSpan? SafeRetentionPeriod { get; }

        Task<long> CalculateSizeAsync();
        Task<long> PurgeAsync();
    }

    public abstract class DevCleanupRuleBase : IDevCleanupRule
    {
        public abstract string Name { get; }
        public abstract string Category { get; }
        public abstract string Description { get; }
        public virtual bool IsDefaultSelected => true;

        protected int? GetRetentionDays()
        {
            int index = SettingsEngine.DevCacheRetentionIndex;

            return index switch
            {
                0 => 7,
                1 => 14,
                2 => 30,
                3 => 90,
                _ => null // Always purge
            };
        }

        protected string GetRetentionText()
        {
            var days = GetRetentionDays();
            return days.HasValue
                ? $"Retains artifacts used in the last {days} days."
                : "Aggressively purges all artifacts regardless of age.";
        }

        public virtual TimeSpan? SafeRetentionPeriod
        {
            get
            {
                var days = GetRetentionDays();
                return days.HasValue ? TimeSpan.FromDays(days.Value) : null;
            }
        }

        protected abstract string[] TargetPaths { get; }

        protected IEnumerable<string> ResolveValidPaths()
        {
            foreach (var path in TargetPaths)
            {
                var expanded = Environment.ExpandEnvironmentVariables(path);

                if (!expanded.Contains('*'))
                {
                    if (Directory.Exists(expanded)) yield return expanded;
                    continue;
                }

                var parts = expanded.Split('*');
                if (parts.Length == 2)
                {
                    var baseDir = parts[0].TrimEnd('\\', '/');
                    var suffix = parts[1].TrimStart('\\', '/');

                    if (Directory.Exists(baseDir))
                    {
                        foreach (var subDir in Directory.GetDirectories(baseDir))
                        {
                            var fullPath = Path.Combine(subDir, suffix);
                            if (Directory.Exists(fullPath)) yield return fullPath;
                        }
                    }
                }
            }
        }

        public async Task<long> CalculateSizeAsync()
        {
            long totalSize = 0;
            await Task.Run(() =>
            {
                var cutoff = SafeRetentionPeriod.HasValue
                    ? DateTime.Now.Subtract(SafeRetentionPeriod.Value)
                    : DateTime.MinValue;

                foreach (var path in ResolveValidPaths())
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(path);
                        if (!dirInfo.Exists) continue;

                        totalSize += dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
                                            .Where(file => !SafeRetentionPeriod.HasValue || file.LastWriteTime < cutoff)
                                            .Sum(file => file.Length);
                    }
                    catch (UnauthorizedAccessException) { /* Ignore locked/system folders */ }
                    catch (DirectoryNotFoundException) { /* Ignore deleted folders */ }
                }
            });
            return totalSize;
        }

        public async Task<long> PurgeAsync()
        {
            long bytesFreed = 0;
            await Task.Run(() =>
            {
                var cutoff = SafeRetentionPeriod.HasValue
                    ? DateTime.Now.Subtract(SafeRetentionPeriod.Value)
                    : DateTime.MinValue;

                foreach (var path in ResolveValidPaths())
                {
                    var dirInfo = new DirectoryInfo(path);
                    if (!dirInfo.Exists) continue;

                    foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            if (SafeRetentionPeriod.HasValue && file.LastWriteTime >= cutoff)
                                continue;

                            long size = file.Length;
                            file.Delete();
                            bytesFreed += size;
                        }
                        catch (Exception) { /* Skip locked/in-use files */ }
                    }

                    var directories = dirInfo.GetDirectories("*", SearchOption.AllDirectories)
                                             .OrderByDescending(d => d.FullName.Length);

                    foreach (var dir in directories)
                    {
                        try
                        {
                            if (!dir.EnumerateFileSystemInfos().Any()) dir.Delete();
                        }
                        catch (Exception) { /* Skip if not empty or locked */ }
                    }
                }
            });
            return bytesFreed;
        }
    }

    #endregion

    #region Package Manager Ecosystem Rules (Updated with Feature 3)

    public class NodeJsRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_node_name") ?? "Node.js (npm, Yarn, pnpm)";
        public override string Category => ResourceString.GetString("dev_cat_js") ?? "JavaScript Ecosystem";
        public override string Description => $"Global package caches. {GetRetentionText()}";
        protected override string[] TargetPaths => new[] { @"%LocalAppData%\npm-cache", @"%LocalAppData%\Yarn\Cache", @"%LocalAppData%\pnpm\store" };
    }

    public class RustRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_rust_name") ?? "Rust (Cargo)";
        public override string Category => ResourceString.GetString("dev_cat_systems") ?? "Systems Programming";
        public override string Description => $"Cargo registry downloads. {GetRetentionText()}";
        protected override string[] TargetPaths => new[] { @"%USERPROFILE%\.cargo\registry\cache", @"%USERPROFILE%\.cargo\git\db" };
    }

    public class PythonRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_python_name") ?? "Python (pip, uv)";
        public override string Category => ResourceString.GetString("dev_cat_python") ?? "Python Ecosystem";
        public override string Description => ResourceString.GetString("dev_python_desc") ?? "Downloaded whl/tar.gz caches. Retains packages used in the last 30 days.";
        protected override string[] TargetPaths => new[] { @"%LocalAppData%\pip\cache", @"%LocalAppData%\uv\cache" };
    }

    public class DotNetRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_dotnet_name") ?? ".NET (NuGet)";
        public override string Category => ResourceString.GetString("dev_cat_dotnet") ?? ".NET Ecosystem";
        public override string Description => ResourceString.GetString("dev_dotnet_desc") ?? "Global NuGet caches. Retains packages used in the last 30 days.";
        protected override string[] TargetPaths => new[] { @"%USERPROFILE%\.nuget\packages", @"%LocalAppData%\NuGet\v3-cache" };
    }

    public class GoRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_go_name") ?? "Go (Build & Mod)";
        public override string Category => ResourceString.GetString("dev_cat_systems") ?? "Systems Programming";
        public override string Description => ResourceString.GetString("dev_go_desc") ?? "Go compiler caches. Retains packages used in the last 30 days.";
        protected override string[] TargetPaths => new[] { @"%LocalAppData%\go-build", @"%USERPROFILE%\go\pkg\mod\cache" };
    }

    public class CppRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_cpp_name") ?? "C/C++ (vcpkg, ccache)";
        public override string Category => ResourceString.GetString("dev_cat_systems") ?? "Systems Programming";
        public override string Description => ResourceString.GetString("dev_cpp_desc") ?? "Build acceleration caches. Retains packages used in the last 30 days.";
        protected override string[] TargetPaths => new[] { @"%LocalAppData%\vcpkg\archives", @"%USERPROFILE%\.vcpkg\archives", @"%LocalAppData%\ccache", @"%LocalAppData%\Mozilla\sccache" };
    }

    public class JavaRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_java_name") ?? "Java (Gradle, Maven)";
        public override string Category => ResourceString.GetString("dev_cat_jvm") ?? "JVM Ecosystem";
        public override string Description => ResourceString.GetString("dev_java_desc") ?? "Gradle/Maven caches. Retains artifacts used in the last 30 days.";
        public override bool IsDefaultSelected => false;
        protected override string[] TargetPaths => new[] { @"%USERPROFILE%\.gradle\caches", @"%USERPROFILE%\.m2\repository" };
    }

    public class VisualStudioRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_vs_name") ?? "Visual Studio MSBuild";
        public override string Category => ResourceString.GetString("dev_cat_ide") ?? "IDE & Tooling";
        public override string Description => ResourceString.GetString("dev_vs_desc") ?? "Temporary MSBuild artifacts. Performs aggressive wipe to fix IDE corruption.";
        protected override string[] TargetPaths => new[] { @"%LocalAppData%\Microsoft\VisualStudio\*\ComponentModelCache" };
    }

    public class JetBrainsRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_jb_name") ?? "JetBrains IDEs";
        public override string Category => ResourceString.GetString("dev_cat_ide") ?? "IDE & Tooling";
        public override string Description => ResourceString.GetString("dev_jb_desc") ?? "System caches and indexing data for Rider, IntelliJ, WebStorm, etc.";
        protected override string[] TargetPaths => new[] { @"%LocalAppData%\JetBrains\*\caches" };
    }

    #endregion

    #region Heavyweight Platforms

    public class VSCodeRule : DevCleanupRuleBase
    {
        public override string Name => "Visual Studio Code";
        public override string Category => "IDE & Tooling";
        public override string Description => "Clears old workspace storage, cached IntelliSense databases, and Chromium temp files.";
        protected override string[] TargetPaths => new[] { @"%AppData%\Code\User\workspaceStorage", @"%AppData%\Code\Cache" };
    }

    public class AndroidStudioRule : DevCleanupRuleBase
    {
        public override string Name => "Android Studio & Flutter";
        public override string Category => "Mobile Ecosystem";
        public override string Description => "Cleans old Android Emulator (AVD) temp data, Android Studio caches, and Flutter Pub cache.";
        public override bool IsDefaultSelected => false;
        protected override string[] TargetPaths => new[] { @"%USERPROFILE%\.android\avd\*\*.lock", @"%USERPROFILE%\.android\cache", @"%LocalAppData%\Google\AndroidStudio*\caches", @"%LocalAppData%\Pub\Cache" };
    }

    public class DockerRule : IDevCleanupRule
    {
        public string Name => "Docker (Containers & Images)";
        public string Category => "Containers & Virtualization";
        public string Description => "Runs 'docker system prune -f' to remove dangling images, stopped containers, and unused build caches.";
        public bool IsDefaultSelected => false;
        public TimeSpan? SafeRetentionPeriod => null;

        public async Task<long> CalculateSizeAsync()
        {
            return await Task.Run(() =>
            {
                string wslPath = Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Docker\wsl\data\ext4.vhdx");
                return File.Exists(wslPath) ? new FileInfo(wslPath).Length : 0;
            });
        }

        public async Task<long> PurgeAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var proc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "docker",
                        Arguments = "system prune -f",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    proc?.WaitForExit();
                    return 0;
                }
                catch { return 0; }
            });
        }
    }

    #endregion

    #region SDK & Toolchain Orphans

    public class DotNetWorkloadRule : IDevCleanupRule
    {
        public string Name => ".NET Orphaned Workloads";
        public string Category => ".NET Ecosystem";
        public string Description => "Executes 'dotnet workload clean' to remove orphaned compiler workloads from older SDK installations.";
        public bool IsDefaultSelected => false;
        public TimeSpan? SafeRetentionPeriod => null;

        public Task<long> CalculateSizeAsync() => Task.FromResult(0L);

        public async Task<long> PurgeAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var proc = Process.Start(new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "workload clean",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    proc?.WaitForExit();
                    return 0;
                }
                catch { return 0; }
            });
        }
    }

    #endregion

    #region Deep Project Sweeping (Dynamic Rules)

    public class DynamicProjectRule : IDevCleanupRule
    {
        public string Name { get; }
        public string Category { get; }
        public string Description { get; }
        public bool IsDefaultSelected => false;
        public TimeSpan? SafeRetentionPeriod => null;

        private readonly string _targetPath;

        public DynamicProjectRule(string name, string category, string description, string targetPath)
        {
            Name = name;
            Category = category;
            Description = description;
            _targetPath = targetPath;
        }

        public async Task<long> CalculateSizeAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    return new DirectoryInfo(_targetPath)
                        .EnumerateFiles("*", SearchOption.AllDirectories)
                        .Sum(f => f.Length);
                }
                catch { return 0; }
            });
        }

        public async Task<long> PurgeAsync()
        {
            long size = await CalculateSizeAsync();
            await Task.Run(() =>
            {
                try { Directory.Delete(_targetPath, true); } catch { }
            });
            return size;
        }
    }

    #endregion

    #region Developer Cleanup Engine

    public class DevCleanupEngine
    {
        public IReadOnlyList<IDevCleanupRule> GetRules()
        {
            return new List<IDevCleanupRule>
            {
                new NodeJsRule(),
                new RustRule(),
                new PythonRule(),
                new DotNetRule(),
                new GoRule(),
                new CppRule(),
                new JavaRule(),
                new VisualStudioRule(),
                new JetBrainsRule(),
                new VSCodeRule(),
                new AndroidStudioRule(),
                new DockerRule(),
                new DotNetWorkloadRule()
            };
        }

        public async Task<List<IDevCleanupRule>> GetDeepProjectRulesAsync(string rootPath)
        {
            var heavyFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "node_modules", "bin", "obj", "target", ".vs", ".nx"
            };
            var foundRules = new List<IDevCleanupRule>();

            await Task.Run(() =>
            {
                try
                {
                    var dirInfo = new DirectoryInfo(rootPath);
                    var directories = dirInfo.GetDirectories("*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 4 });

                    foreach (var dir in directories)
                    {
                        if (heavyFolderNames.Contains(dir.Name))
                        {
                            foundRules.Add(new DynamicProjectRule(
                                name: $"{dir.Parent?.Name ?? "Project"} ({dir.Name})",
                                category: "Deep Project Sweep",
                                description: $"Found heavy artifact folder at: {dir.FullName}",
                                targetPath: dir.FullName
                            ));
                        }
                    }
                }
                catch { /* Ignore access exceptions */ }
            });

            return foundRules;
        }

        public async Task OptimizeGitRepositoriesAsync(string rootPath)
        {
            await Task.Run(() =>
            {
                try
                {
                    var gitFolders = new DirectoryInfo(rootPath)
                        .GetDirectories(".git", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 4 });

                    foreach (var gitDir in gitFolders)
                    {
                        var repoRoot = gitDir.Parent?.FullName;
                        if (string.IsNullOrEmpty(repoRoot)) continue;

                        try
                        {
                            var proc = Process.Start(new ProcessStartInfo
                            {
                                FileName = "git",
                                Arguments = "gc --prune=now",
                                WorkingDirectory = repoRoot,
                                CreateNoWindow = true,
                                UseShellExecute = false
                            });
                            proc?.WaitForExit(30000);
                        }
                        catch { /* Ignore if git is not installed or repo is locked */ }
                    }
                }
                catch { }
            });
        }
    }

    #endregion
}