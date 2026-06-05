// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Services;

public sealed class ThemeWallpaperApplier(
    IWallpaperService wallpaperService,
    IWindowsVersionService versionService,
    IWindowsRegistryService registryService,
    ILogService logService,
    IFileSystemService fileSystemService) : ISpecialSettingHandler
{
    public async Task<bool> TryApplySpecialSettingAsync(
        SettingDefinition setting,
        object value,
        bool additionalContext = false,
        ISettingApplicationService? settingApplicationService = null)
    {
        if (setting.Id != SettingIds.ThemeModeWindows) return false;
        if (value is not int selectionIndex) return false;

        logService.Log(LogLevel.Info,
            $"[ThemeWallpaperApplier] Applying theme mode - Index: {selectionIndex}, ApplyWallpaper: {additionalContext}");

        int themeValue = selectionIndex == 1 ? 0 : 1;
        if (setting.RegistrySettings != null)
        {
            foreach (var registrySetting in setting.RegistrySettings)
                registryService.ApplySetting(registrySetting, true, themeValue);
        }

        if (additionalContext)
        {
            try
            {
                var isDarkMode = selectionIndex == 1;
                var isWindows11 = versionService.IsWindows11();
                var wallpaperPath = WindowsThemeCustomizations.Wallpaper.GetDefaultWallpaperPath(isWindows11, isDarkMode);

                if (fileSystemService.FileExists(wallpaperPath))
                {
                    await wallpaperService.SetWallpaperAsync(wallpaperPath).ConfigureAwait(false);
                    logService.Log(LogLevel.Info, $"[ThemeWallpaperApplier] Wallpaper changed to: {wallpaperPath}");
                }
            }
            catch (System.Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[ThemeWallpaperApplier] Failed to change wallpaper: {ex.Message}");
            }
        }

        return true;
    }
}
