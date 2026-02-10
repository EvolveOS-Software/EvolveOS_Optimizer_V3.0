using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.Win32;
using System.IO;
using System.Threading;

namespace EvolveOS_Optimizer.Utilities.Helpers;

public static class GroupPolicyHelper
{
    public sealed record PolicyEntry
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required string Category { get; init; }
        public required RegistryHive Hive { get; init; }
        public required string RegistryPath { get; init; }
        public required string ValueName { get; init; }
        public required RegistryValueKind ValueKind { get; init; }
        public int MinWindowsBuild { get; init; }
        public int MaxWindowsBuild { get; init; }
    }

    public sealed record PolicyState
    {
        public required PolicyEntry Policy { get; init; }
        public required bool IsConfigured { get; init; }
        public object? CurrentValue { get; init; }
        public RegistryValueKind? ActualValueKind { get; init; }
    }

    private static int GetWindowsBuildNumber()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key?.GetValue("CurrentBuildNumber") is string buildStr && int.TryParse(buildStr, out var build))
            {
                return build;
            }
        }
        catch
        {
            // Ignore errors
        }
        return 0;
    }

    public static IReadOnlyList<PolicyEntry> GetApplicablePolicies()
    {
        var currentBuild = GetWindowsBuildNumber();

        return GroupPolicyData.KnownPolicies
            .Where(p => (p.MinWindowsBuild == 0 || currentBuild >= p.MinWindowsBuild) &&
                        (p.MaxWindowsBuild == 0 || currentBuild <= p.MaxWindowsBuild))
            .ToList()
            .AsReadOnly();
    }

    public static async Task<IReadOnlyList<PolicyState>> DetectPolicyStatesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var results = new List<PolicyState>();
            var applicablePolicies = GetApplicablePolicies();

            foreach (var policy in applicablePolicies)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var state = DetectPolicyState(policy);
                results.Add(state);
            }

            return (IReadOnlyList<PolicyState>)results.AsReadOnly();
        }, cancellationToken).ConfigureAwait(false);
    }

    private static PolicyState DetectPolicyState(PolicyEntry policy)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(policy.Hive,
                Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default);
            using var subKey = baseKey.OpenSubKey(policy.RegistryPath, writable: false);

            if (subKey == null)
            {
                return new PolicyState
                {
                    Policy = policy,
                    IsConfigured = false,
                    CurrentValue = null,
                    ActualValueKind = null
                };
            }

            var value = subKey.GetValue(policy.ValueName);
            if (value == null)
            {
                return new PolicyState
                {
                    Policy = policy,
                    IsConfigured = false,
                    CurrentValue = null,
                    ActualValueKind = null
                };
            }

            var actualKind = subKey.GetValueKind(policy.ValueName);
            return new PolicyState
            {
                Policy = policy,
                IsConfigured = true,
                CurrentValue = value,
                ActualValueKind = actualKind
            };
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            return new PolicyState
            {
                Policy = policy,
                IsConfigured = false,
                CurrentValue = null,
                ActualValueKind = null
            };
        }
    }

    public static async Task<bool> RemovePolicyOverrideAsync(PolicyEntry policy)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(policy.Hive,
                    Environment.Is64BitOperatingSystem ? Microsoft.Win32.RegistryView.Registry64 : Microsoft.Win32.RegistryView.Default);
                using var subKey = baseKey.OpenSubKey(policy.RegistryPath, writable: true);

                if (subKey == null)
                {
                    return true;
                }

                if (subKey.GetValue(policy.ValueName) != null)
                {
                    subKey.DeleteValue(policy.ValueName, throwOnMissingValue: false);
                }

                CleanupEmptyPolicyKey(policy.Hive, policy.RegistryPath);

                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                ErrorLogging.LogDebug(ex);
                return false;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                ErrorLogging.LogWritingFile(ex);
                return false;
            }
        }).ConfigureAwait(false);
    }

    public static async Task<(int succeeded, int failed)> RemovePolicyOverridesAsync(
        IEnumerable<PolicyEntry> policies,
        IProgress<(string policyId, bool success)>? progress = null,
        System.Threading.CancellationToken cancellationToken = default)
    {
        var succeeded = 0;
        var failed = 0;

        foreach (var policy in policies)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var success = await RemovePolicyOverrideAsync(policy).ConfigureAwait(false);
            if (success)
                succeeded++;
            else
                failed++;

            progress?.Report((policy.Id, success));
        }

        return (succeeded, failed);
    }

    private static void CleanupEmptyPolicyKey(RegistryHive hive, string path)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive,
                Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default);
            using var subKey = baseKey.OpenSubKey(path, writable: false);

            if (subKey == null)
                return;

            if (subKey.GetValueNames().Length == 0 && subKey.GetSubKeyNames().Length == 0)
            {
                var parentPath = GetParentPath(path);
                if (!string.IsNullOrEmpty(parentPath) && parentPath.Contains("Policies", StringComparison.OrdinalIgnoreCase))
                {
                    using var parentKey = baseKey.OpenSubKey(parentPath, writable: true);
                    var keyName = Path.GetFileName(path);
                    parentKey?.DeleteSubKey(keyName, throwOnMissingSubKey: false);
                }
            }
        }
        catch
        {
            // Ignore cleanup errors - not critical
        }
    }

    private static string GetParentPath(string path)
    {
        var lastSeparator = path.LastIndexOf('\\');
        return lastSeparator > 0 ? path[..lastSeparator] : string.Empty;
    }

    public static async Task<IReadOnlyList<PolicyState>> GetConfiguredPoliciesAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        var allStates = await DetectPolicyStatesAsync(cancellationToken).ConfigureAwait(false);
        return allStates.Where(s => s.IsConfigured).ToList().AsReadOnly();
    }

    public static async Task<IReadOnlyDictionary<string, (int total, int configured)>> GetPolicySummaryAsync(
        System.Threading.CancellationToken cancellationToken = default)
    {
        var allStates = await DetectPolicyStatesAsync(cancellationToken).ConfigureAwait(false);

        return allStates
            .GroupBy(s => s.Policy.Category)
            .ToDictionary(
                g => g.Key,
                g => (total: g.Count(), configured: g.Count(s => s.IsConfigured)))
            .AsReadOnly();
    }

    public static async Task RestartExplorerAsync()
    {
        try
        {
            await CommandExecutor.GetCommandOutput("taskkill /F /IM explorer.exe", false).ConfigureAwait(false);
            await Task.Delay(500).ConfigureAwait(false);
            await CommandExecutor.GetCommandOutput("start explorer.exe", false).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            ErrorLogging.LogWritingFile(ex);
        }
    }
}
