// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.Concurrent;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Utilities.Services;

public class GlobalSettingsRegistry : IGlobalSettingsRegistry
{
    #region Fields & Constructor

    private readonly ConcurrentDictionary<string, List<ISettingItem>> _moduleSettings;
    private readonly ILogService _logService;
    private readonly object _listLock = new();

    public GlobalSettingsRegistry(ILogService logService)
    {
        _moduleSettings = new ConcurrentDictionary<string, List<ISettingItem>>();
        _logService = logService;
    }

    #endregion

    #region Registration

    public void RegisterSettings(string moduleName, IEnumerable<ISettingItem> settings)
    {
        if (string.IsNullOrEmpty(moduleName))
        {
            _logService.Log(
                LogLevel.Warning,
                "Cannot register settings for null or empty module name"
            );
            return;
        }

        var settingsList = settings?.ToList() ?? new List<ISettingItem>();
        _moduleSettings.AddOrUpdate(moduleName, settingsList, (key, oldValue) => settingsList);

        _logService.Log(
            LogLevel.Debug,
            $"Registered {settingsList.Count} settings for module '{moduleName}'"
        );
    }

    public void RegisterSetting(string moduleName, ISettingItem setting)
    {
        if (string.IsNullOrEmpty(moduleName))
        {
            _logService.Log(
                LogLevel.Warning,
                "Cannot register setting for null or empty module name"
            );
            return;
        }

        if (setting == null)
        {
            _logService.Log(LogLevel.Warning, "Cannot register null setting");
            return;
        }

        lock (_listLock)
        {
            _moduleSettings.AddOrUpdate(
                moduleName,
                new List<ISettingItem> { setting },
                (key, existingSettings) =>
                {
                    if (!existingSettings.Any(s => s.Id == setting.Id))
                    {
                        existingSettings.Add(setting);
                        _logService.Log(
                            LogLevel.Debug,
                            $"Added setting '{setting.Id}' to existing module '{moduleName}'"
                        );
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Debug,
                            $"Setting '{setting.Id}' already exists in module '{moduleName}', skipping registration"
                        );
                    }
                    return existingSettings;
                }
            );
        }

        _logService.Log(
            LogLevel.Debug,
            $"Registered setting '{setting.Id}' for module '{moduleName}'"
        );
    }

    #endregion

    #region Retrieval

    public ISettingItem? GetSetting(string settingId, string? moduleName = null)
    {
        if (string.IsNullOrEmpty(settingId))
        {
            _logService.Log(
                LogLevel.Warning,
                "Cannot get setting for null or empty setting ID"
            );
            return null;
        }

        if (!string.IsNullOrEmpty(moduleName))
        {
            if (_moduleSettings.TryGetValue(moduleName, out var moduleSettingsList))
            {
                ISettingItem? setting;
                lock (_listLock)
                {
                    setting = moduleSettingsList.FirstOrDefault(s => s.Id == settingId);
                }
                if (setting != null)
                {
                    _logService.Log(
                        LogLevel.Debug,
                        $"Found setting '{settingId}' in module '{moduleName}'"
                    );
                    return setting;
                }
            }
            _logService.Log(
                LogLevel.Debug,
                $"Setting '{settingId}' not found in module '{moduleName}'"
            );
            return null;
        }

        foreach (var kvp in _moduleSettings)
        {
            ISettingItem? setting;
            lock (_listLock)
            {
                setting = kvp.Value.FirstOrDefault(s => s.Id == settingId);
            }
            if (setting != null)
            {
                _logService.Log(
                    LogLevel.Debug,
                    $"Found setting '{settingId}' in module '{kvp.Key}'"
                );
                return setting;
            }
        }

        _logService.Log(LogLevel.Debug, $"Setting '{settingId}' not found in any module");
        return null;
    }

    public IEnumerable<ISettingItem> GetAllSettings()
    {
        lock (_listLock)
        {
            return _moduleSettings.Values
                .SelectMany(settings => settings)
                .ToList();
        }
    }

    #endregion
}
