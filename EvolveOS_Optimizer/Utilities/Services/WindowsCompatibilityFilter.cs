// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.Concurrent;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Services;

/// <summary>
/// Evaluates and filters application settings based on the underlying Windows Operating System version.
/// </summary>
/// <remarks>
/// <para>
/// <b>Responsibility:</b>
/// This service ensures that users are only presented with settings that are compatible with their 
/// specific machine. It acts as a gatekeeper to prevent the application from attempting to modify 
/// registry keys, APIs, or behaviors that do not exist or function differently on unsupported OS builds.
/// </para>
/// <para>
/// <b>Capabilities:</b>
/// It evaluates fine-grained criteria including:
/// <list type="bullet">
/// <item>Major OS version gates (Windows 10 vs Windows 11).</item>
/// <item>Specific Minimum and Maximum build/revision ranges (e.g., separating 21H2 from 22H2 features).</item>
/// <item>OS SKU mapping (e.g., mapping Windows Server builds to their desktop equivalents for feature parity).</item>
/// <item>UI Decoration: It can either strictly filter out incompatible settings (hiding them) or decorate them with localization keys so the UI can display them as "disabled due to compatibility" with an explanatory message.</item>
/// </list>
/// </para>
/// </remarks>
public class WindowsCompatibilityFilter : IWindowsCompatibilityFilter
{
    #region Fields & Constructor

    private readonly IWindowsVersionService _versionService;
    private readonly ILogService _logService;
    private readonly ConcurrentDictionary<string, byte> _loggedCompatibilityMessages = new();

    public WindowsCompatibilityFilter(
        IWindowsVersionService versionService,
        ILogService logService)
    {
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
    }

    #endregion

    #region Public API

    public virtual IEnumerable<SettingDefinition> FilterSettingsByWindowsVersion(
        IEnumerable<SettingDefinition> settings)
    {
        return FilterSettingsByWindowsVersion(settings, applyFilter: true);
    }

    public virtual IEnumerable<SettingDefinition> FilterSettingsByWindowsVersion(
        IEnumerable<SettingDefinition> settings,
        bool applyFilter)
    {
        if (!applyFilter)
        {
            return DecorateSettingsWithCompatibilityMessages(settings);
        }

        try
        {
            var isWindows11 = _versionService.IsWindows11();
            var buildNumber = _versionService.GetWindowsBuildNumber();
            var buildRevision = _versionService.GetWindowsBuildRevision();
            var isServer = _versionService.IsWindowsServer();

            if (isServer)
            {
                _logService.Log(LogLevel.Info,
                    $"Windows Server detected (build {buildNumber}). Treating as Windows {(isWindows11 ? "11" : "10")} for compatibility filtering.");
            }

            _logService.Log(LogLevel.Debug,
                $"Filtering settings for Windows {(isWindows11 ? "11" : "10")}{(isServer ? " Server" : "")} build {buildNumber}");

            var compatibleSettings = new List<SettingDefinition>();
            var filteredCount = 0;

            foreach (var setting in settings)
            {
                bool isCompatible = true;
                string incompatibilityReason = "";

                bool isWindows10Only = false;
                bool isWindows11Only = false;
                int? minimumBuild = null;
                int? minimumRevision = null;
                int? maximumBuild = null;
                int? maximumRevision = null;
                IReadOnlyList<(int MinBuild, int MaxBuild)>? supportedRanges = null;

                if (setting is SettingDefinition appSetting)
                {
                    isWindows10Only = appSetting.IsWindows10Only;
                    isWindows11Only = appSetting.IsWindows11Only;
                    minimumBuild = appSetting.MinimumBuildNumber;
                    minimumRevision = appSetting.MinimumBuildRevision;
                    maximumBuild = appSetting.MaximumBuildNumber;
                    maximumRevision = appSetting.MaximumBuildRevision;
                    supportedRanges = appSetting.SupportedBuildRanges;
                }

                if (isWindows10Only && isWindows11)
                {
                    isCompatible = false;
                    incompatibilityReason = "Windows 10 only";
                }
                else if (isWindows11Only && !isWindows11)
                {
                    isCompatible = false;
                    incompatibilityReason = "Windows 11 only";
                }
                else if (supportedRanges?.Count > 0)
                {
                    bool inSupportedRange = supportedRanges.Any(range =>
                        buildNumber >= range.MinBuild && buildNumber <= range.MaxBuild);

                    if (!inSupportedRange)
                    {
                        isCompatible = false;
                        var rangesStr = string.Join(", ", supportedRanges.Select(r => $"{r.MinBuild}-{r.MaxBuild}"));
                        incompatibilityReason = $"build not in supported ranges: {rangesStr}";
                    }
                }
                else
                {
                    if (minimumBuild.HasValue)
                    {
                        if (buildNumber < minimumBuild.Value)
                        {
                            isCompatible = false;
                            incompatibilityReason = $"requires build >= {minimumBuild.Value}";
                        }
                        else if (buildNumber == minimumBuild.Value && minimumRevision.HasValue && buildRevision < minimumRevision.Value)
                        {
                            isCompatible = false;
                            incompatibilityReason = $"requires build >= {minimumBuild.Value}.{minimumRevision.Value}";
                        }
                    }

                    if (isCompatible && maximumBuild.HasValue)
                    {
                        if (buildNumber > maximumBuild.Value)
                        {
                            isCompatible = false;
                            incompatibilityReason = $"requires build <= {maximumBuild.Value}";
                        }
                        else if (buildNumber == maximumBuild.Value && maximumRevision.HasValue && buildRevision > maximumRevision.Value)
                        {
                            isCompatible = false;
                            incompatibilityReason = $"requires build <= {maximumBuild.Value}.{maximumRevision.Value}";
                        }
                    }
                }

                if (isCompatible)
                {
                    compatibleSettings.Add(setting);
                }
                else
                {
                    filteredCount++;
                    _logService.Log(LogLevel.Debug,
                        $"Filtered out setting '{setting.Id}': {incompatibilityReason}");
                }
            }

            if (filteredCount > 0)
            {
                _logService.Log(LogLevel.Debug,
                    $"Filtered out {filteredCount} incompatible settings. {compatibleSettings.Count} settings remain.");
            }

            return compatibleSettings;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error,
                $"Error filtering settings by Windows version: {ex.Message}. Returning all settings.");
            return settings;
        }
    }

    #endregion

    #region Setting Decoration (Non-Destructive Filtering)

    private IEnumerable<SettingDefinition> DecorateSettingsWithCompatibilityMessages(
        IEnumerable<SettingDefinition> settings)
    {
        var isWindows11 = _versionService.IsWindows11();
        var buildNumber = _versionService.GetWindowsBuildNumber();
        var buildRevision = _versionService.GetWindowsBuildRevision();

        foreach (var setting in settings)
        {
            string? compatibilityMessage = null;

            if (setting.IsWindows10Only && isWindows11)
            {
                compatibilityMessage = "Compatibility_Windows10Only";
            }
            else if (setting.IsWindows11Only && !isWindows11)
            {
                compatibilityMessage = "Compatibility_Windows11Only";
            }
            else if (setting.MinimumBuildNumber.HasValue &&
                     buildNumber < setting.MinimumBuildNumber.Value)
            {
                compatibilityMessage = $"Compatibility_MinBuild|{setting.MinimumBuildNumber.Value}";
            }
            else if (setting.MinimumBuildNumber.HasValue &&
                     buildNumber == setting.MinimumBuildNumber.Value &&
                     setting.MinimumBuildRevision.HasValue &&
                     buildRevision < setting.MinimumBuildRevision.Value)
            {
                compatibilityMessage = $"Compatibility_MinBuild|{setting.MinimumBuildNumber.Value}.{setting.MinimumBuildRevision.Value}";
            }
            else if (setting.MaximumBuildNumber.HasValue &&
                     buildNumber > setting.MaximumBuildNumber.Value)
            {
                compatibilityMessage = $"Compatibility_MaxBuild|{setting.MaximumBuildNumber.Value}";
            }
            else if (setting.MaximumBuildNumber.HasValue &&
                     buildNumber == setting.MaximumBuildNumber.Value &&
                     setting.MaximumBuildRevision.HasValue &&
                     buildRevision > setting.MaximumBuildRevision.Value)
            {
                compatibilityMessage = $"Compatibility_MaxBuild|{setting.MaximumBuildNumber.Value}.{setting.MaximumBuildRevision.Value}";
            }
            else if (setting.SupportedBuildRanges?.Count > 0)
            {
                bool inRange = setting.SupportedBuildRanges.Any(range =>
                    buildNumber >= range.MinBuild && buildNumber <= range.MaxBuild);

                if (!inRange)
                {
                    var rangeText = string.Join(" or ",
                        setting.SupportedBuildRanges.Select(r => $"{r.MinBuild}-{r.MaxBuild}"));
                    compatibilityMessage = $"Compatibility_BuildRange|{rangeText}";
                }
            }

            if (compatibilityMessage != null)
            {
                var logKey = $"{setting.Name}:{compatibilityMessage}";
                if (_loggedCompatibilityMessages.TryAdd(logKey, 0))
                {
                    _logService.Log(LogLevel.Info, $"Adding compatibility message to {setting.Name}: {compatibilityMessage}");
                }

                yield return setting with { VersionCompatibilityMessage = compatibilityMessage };
            }
            else
            {
                yield return setting;
            }
        }
    }

    #endregion
}