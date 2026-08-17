// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.Maintenance
{
    public interface IDevCleanupRule
    {
        string Name { get; }
        string Category { get; }
        string Description { get; }
        bool IsDefaultSelected { get; }

        Task<long> CalculateSizeAsync();
        Task<long> PurgeAsync();
    }

    public abstract class DevCleanupRuleBase : IDevCleanupRule
    {
        public abstract string Name { get; }
        public abstract string Category { get; }
        public abstract string Description { get; }
        public virtual bool IsDefaultSelected => true;

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
                foreach (var path in ResolveValidPaths())
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(path);
                        totalSize += dirInfo.EnumerateFiles("*", SearchOption.AllDirectories)
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
                foreach (var path in ResolveValidPaths())
                {
                    var dirInfo = new DirectoryInfo(path);
                    if (!dirInfo.Exists) continue;

                    foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            long size = file.Length;
                            file.Delete();
                            bytesFreed += size;
                        }
                        catch (Exception) { /* Skip locked/in-use files (e.g., active IDE indexing) */ }
                    }

                    var directories = dirInfo.GetDirectories("*", SearchOption.AllDirectories)
                                             .OrderByDescending(d => d.FullName.Length);

                    foreach (var dir in directories)
                    {
                        try { dir.Delete(); }
                        catch (Exception) { /* Skip if not empty or locked */ }
                    }
                }
            });
            return bytesFreed;
        }
    }

    #region Specific Ecosystem Rules

    public class NodeJsRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_node_name") ?? "Node.js (npm, Yarn, pnpm)";
        public override string Category => ResourceString.GetString("dev_cat_js") ?? "JavaScript Ecosystem";
        public override string Description => ResourceString.GetString("dev_node_desc") ?? "Global package caches for npm, Yarn, and pnpm package managers.";
        protected override string[] TargetPaths => new[] { @"%LocalAppData%\npm-cache", @"%LocalAppData%\Yarn\Cache", @"%LocalAppData%\pnpm\store" };
    }

    public class RustRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_rust_name") ?? "Rust (Cargo)";
        public override string Category => ResourceString.GetString("dev_cat_systems") ?? "Systems Programming";
        public override string Description => ResourceString.GetString("dev_rust_desc") ?? "Cargo registry downloads and git database cache.";
        protected override string[] TargetPaths => new[] { @"%USERPROFILE%\.cargo\registry\cache", @"%USERPROFILE%\.cargo\git\db" };
    }

    public class PythonRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_python_name") ?? "Python (pip, uv)";
        public override string Category => ResourceString.GetString("dev_cat_python") ?? "Python Ecosystem";
        public override string Description => ResourceString.GetString("dev_python_desc") ?? "Downloaded whl and tar.gz package caches.";
        protected override string[] TargetPaths => new[] { @"%LocalAppData%\pip\cache", @"%LocalAppData%\uv\cache" };
    }

    public class DotNetRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_dotnet_name") ?? ".NET (NuGet)";
        public override string Category => ResourceString.GetString("dev_cat_dotnet") ?? ".NET Ecosystem";
        public override string Description => ResourceString.GetString("dev_dotnet_desc") ?? "Global NuGet package caches and HTTP download cache.";
        protected override string[] TargetPaths => new[] { @"%USERPROFILE%\.nuget\packages", @"%LocalAppData%\NuGet\v3-cache" };
    }

    public class GoRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_go_name") ?? "Go (Build & Mod)";
        public override string Category => ResourceString.GetString("dev_cat_systems") ?? "Systems Programming";
        public override string Description => ResourceString.GetString("dev_go_desc") ?? "Go compiler build cache and module cache.";
        protected override string[] TargetPaths => new[] { @"%LocalAppData%\go-build", @"%USERPROFILE%\go\pkg\mod\cache" };
    }

    public class CppRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_cpp_name") ?? "C/C++ (vcpkg, ccache)";
        public override string Category => ResourceString.GetString("dev_cat_systems") ?? "Systems Programming";
        public override string Description => ResourceString.GetString("dev_cpp_desc") ?? "Vcpkg archives and sccache/ccache build acceleration caches.";
        protected override string[] TargetPaths => new[] { @"%LocalAppData%\vcpkg\archives", @"%USERPROFILE%\.vcpkg\archives", @"%LocalAppData%\ccache", @"%LocalAppData%\Mozilla\sccache" };
    }

    public class JavaRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_java_name") ?? "Java (Gradle, Maven)";
        public override string Category => ResourceString.GetString("dev_cat_jvm") ?? "JVM Ecosystem";
        public override string Description => ResourceString.GetString("dev_java_desc") ?? "Gradle caches and Maven local repository. (Clearing Maven requires redownloading artifacts).";
        public override bool IsDefaultSelected => false;
        protected override string[] TargetPaths => new[] { @"%USERPROFILE%\.gradle\caches", @"%USERPROFILE%\.m2\repository" };
    }

    public class VisualStudioRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_vs_name") ?? "Visual Studio MSBuild";
        public override string Category => ResourceString.GetString("dev_cat_ide") ?? "IDE & Tooling";
        public override string Description => ResourceString.GetString("dev_vs_desc") ?? "Component model caches and temporary MSBuild artifacts.";
        protected override string[] TargetPaths => new[] { @"%LocalAppData%\Microsoft\VisualStudio\*\ComponentModelCache" };
    }

    public class JetBrainsRule : DevCleanupRuleBase
    {
        public override string Name => ResourceString.GetString("dev_jb_name") ?? "JetBrains IDEs";
        public override string Category => ResourceString.GetString("dev_cat_ide") ?? "IDE & Tooling";
        public override string Description => ResourceString.GetString("dev_jb_desc") ?? "System caches, old indexing data, and temp files for Rider, IntelliJ, WebStorm, etc.";
        protected override string[] TargetPaths => new[] { @"%LocalAppData%\JetBrains\*\caches" };
    }

    #endregion

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
                new JetBrainsRule()
            };
        }
    }
}