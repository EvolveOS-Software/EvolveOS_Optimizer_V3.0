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

        var storedBaselineStr = _prefs.GetPreference("NewBadgeBaseline", "");
        if (TryParseVersion(storedBaselineStr, out var storedBaseline))
        {
            _baseline = storedBaseline;
        }
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
    public void Initialize(IEnumerable<string?> allAddedInVersions)
    {
        var currentAssemblyVersion = GetAppVersion();
        _prefs.SetPreferenceAsync("LastRunVersion", currentAssemblyVersion);

        Version? highestInRegistry = null;
        if (allAddedInVersions != null)
        {
            foreach (var raw in allAddedInVersions)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                if (!TryParseVersion(raw, out var parsed))
                    continue;
                if (highestInRegistry is null || parsed > highestInRegistry)
                    highestInRegistry = parsed;
            }
        }

        // Use to reset the baseline
        //var storedHighestStr = "0.0.0";
        //var storedBaselineStr = "0.0.0";

        var storedHighestStr = _prefs.GetPreference(UserPreferenceKeys.HighestSeenAddedInVersion, "");
        var storedBaselineStr = _prefs.GetPreference("NewBadgeBaseline", "");

        var highestOk = TryParseVersion(storedHighestStr, out var storedHighest);
        var baselineOk = TryParseVersion(storedBaselineStr, out var storedBaseline);
        if (!highestOk || !baselineOk)
        {
            _baseline = new Version(0, 0, 0);
            if (highestInRegistry is not null)
            {
                _prefs.SetPreferenceAsync(
                    UserPreferenceKeys.HighestSeenAddedInVersion,
                    VersionToString(highestInRegistry));
            }
            _prefs.SetPreferenceAsync("NewBadgeBaseline", VersionToString(_baseline));

            _logService.LogInformation(
                "[NewBadge] Uninitialized or half-populated state. Baseline set to 0.0.0 (all tagged settings treated as new).");
            return;
        }

        if (highestInRegistry is not null && highestInRegistry > storedHighest)
        {
            _baseline = storedHighest;
            _prefs.SetPreferenceAsync(
                UserPreferenceKeys.HighestSeenAddedInVersion,
                VersionToString(highestInRegistry));
            _prefs.SetPreferenceAsync("NewBadgeBaseline", VersionToString(storedHighest));
            ShowNewBadges = true;
            _logService.LogInformation(
                $"[NewBadge] Effective upgrade: registry highest {highestInRegistry} > stored {storedHighest}. " +
                $"Baseline={storedHighest}; ShowNewBadges reset to true.");
            return;
        }

        _baseline = storedBaseline;
        _logService.LogDebug(
            $"[NewBadge] No upgrade. Baseline={_baseline}, ShowNewBadges={ShowNewBadges}.");
    }

    public bool IsSettingNew(string? addedInVersion, string settingId)
    {
        if (string.IsNullOrEmpty(addedInVersion))
            return false;

        if (!TryParseVersion(addedInVersion, out var settingVersion))
            return false;

        return settingVersion > _baseline;
    }
    #endregion

    #region Private Methods
    private static string GetAppVersion()
    {
        var attr = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        var version = attr?.InformationalVersion ?? "0.0.0";

        version = version.Replace("Build:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        version = version.TrimStart('v');

        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
            version = version[..plusIndex];

        return version;
    }

    private static bool TryParseVersion(string versionStr, out Version parsed)
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