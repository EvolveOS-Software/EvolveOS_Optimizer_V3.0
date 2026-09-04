// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Reflection;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Utilities.Services;

public class NewBadgeService : INewBadgeService
{
    #region Fields
    private readonly IUserPreferencesService _prefs;
    private readonly ILogService _logService;
    private Version _baseline = new(0, 0, 0);
    #endregion

    #region Constructor
    public NewBadgeService(IUserPreferencesService prefs, ILogService logService)
    {
        _prefs = prefs;
        _logService = logService;
    }
    #endregion

    #region Properties
    public bool ShowNewBadges
    {
        get => _prefs.GetPreference(UserPreferenceKeys.ShowNewBadges, true);
        set => _prefs.SetPreferenceAsync(UserPreferenceKeys.ShowNewBadges, value);
    }
    #endregion

    #region Public Methods
    public async Task InitializeAsync(IEnumerable<string?> allAddedInVersions)
    {
        var currentAppVersion = GetAppVersion();

        var uniqueVersions = new HashSet<Version>();
        if (allAddedInVersions != null)
        {
            foreach (var raw in allAddedInVersions)
            {
                if (TryParseVersion(raw, out var parsed))
                {
                    uniqueVersions.Add(parsed);
                }
            }
        }
        uniqueVersions.Add(currentAppVersion);

        var sortedVersions = uniqueVersions.OrderBy(v => v).ToList();
        var currentIndex = sortedVersions.IndexOf(currentAppVersion);

        // Calculate the 2-version rolling cutoff (Current version)
        // Example: Calculate the 3-version rolling cutoff
        // Change currentIndex to - 3);
        var cutoffIndex = Math.Max(0, currentIndex - 2);
        var twoVersionCutoff = sortedVersions[cutoffIndex];

        var installBaselineStr = _prefs.GetPreference("InstallBaseline", "");
        Version installBaseline;

        if (!TryParseVersion(installBaselineStr, out installBaseline))
        {
            Version? highestInRegistry = GetHighestVersion(allAddedInVersions);
            if (highestInRegistry is not null)
            {
                installBaseline = new Version(highestInRegistry.Major, highestInRegistry.Minor, 0, 0);
            }
            else
            {
                installBaseline = currentAppVersion;
            }

            await _prefs.SetPreferenceAsync("InstallBaseline", VersionToString(installBaseline)).ConfigureAwait(false);
            _logService.LogInformation($"[NewBadge] Fresh install detected. InstallBaseline set to {installBaseline}.");
        }

        _baseline = twoVersionCutoff > installBaseline ? twoVersionCutoff : installBaseline;

        _logService.LogInformation($"[NewBadge] Current App: {currentAppVersion}. Rolling Cutoff: {twoVersionCutoff}. Active Baseline: {_baseline}.");

        await _prefs.SetPreferenceAsync("LastRunVersion", VersionToString(currentAppVersion)).ConfigureAwait(false);

        await Task.Delay(150).ConfigureAwait(false);
    }

    public bool IsSettingNew(string? addedInVersion, string settingId)
    {
        if (!ShowNewBadges || string.IsNullOrWhiteSpace(addedInVersion))
            return false;

        if (!TryParseVersion(addedInVersion, out var settingVersion))
            return false;

        return settingVersion >= _baseline;
    }
    #endregion

    #region Private Helpers
    private static Version? GetHighestVersion(IEnumerable<string?>? versions)
    {
        Version? highest = null;
        if (versions is null) return null;

        foreach (var raw in versions)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (!TryParseVersion(raw, out var parsed)) continue;

            if (highest is null || parsed > highest)
            {
                highest = parsed;
            }
        }
        return highest;
    }

    private static Version GetAppVersion()
    {
        try
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            var informationalVersion = entryAssembly?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            var versionStr = informationalVersion?.Split(' ').Last().Trim() ?? "1.0.0";

            var plusIndex = versionStr.IndexOf('+');
            if (plusIndex >= 0)
            {
                versionStr = versionStr[..plusIndex];
            }

            if (Version.TryParse(versionStr, out var parsed))
            {
                return parsed;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GetAppVersion] Error parsing version: {ex.Message}");
        }

        return new Version(0, 0, 0);
    }

    private static bool TryParseVersion(string? versionStr, out Version parsed)
    {
        if (string.IsNullOrWhiteSpace(versionStr))
        {
            parsed = new Version(0, 0, 0);
            return false;
        }

        versionStr = versionStr.Trim().TrimStart('v');
        return Version.TryParse(versionStr, out parsed!);
    }

    private static string VersionToString(Version v)
    {
        return v.ToString();
    }
    #endregion
}