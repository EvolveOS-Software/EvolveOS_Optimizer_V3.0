// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Services;

public class WallpaperService : IWallpaperService
{
    private readonly ILogService _logService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IWindowsRegistryService _registryService;
    private readonly ISystemParametersService _systemParametersService;

    private const int SPI_SETDESKWALLPAPER = 0x0014;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDCHANGE = 0x02;

    public WallpaperService(
        ILogService logService,
        IInteractiveUserService interactiveUserService,
        IWindowsRegistryService registryService,
        ISystemParametersService systemParametersService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _interactiveUserService = interactiveUserService;
        _registryService = registryService;
        _systemParametersService = systemParametersService;
    }

    public string GetDefaultWallpaperPath(bool isWindows11, bool isDarkMode)
    {
        return WindowsThemeCustomizations.Wallpaper.GetDefaultWallpaperPath(isWindows11, isDarkMode);
    }

    public Task<bool> SetWallpaperAsync(string wallpaperPath)
    {
        try
        {
            int flags;

            if (_interactiveUserService.IsOtsElevation)
            {
                _registryService.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "Wallpaper", wallpaperPath, Microsoft.Win32.RegistryValueKind.String);

                flags = SPIF_SENDCHANGE;
            }
            else
            {
                flags = SPIF_UPDATEINIFILE | SPIF_SENDCHANGE;
            }

            bool success = _systemParametersService.SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, wallpaperPath, flags) != 0;

            if (success)
            {
                _logService.Log(LogLevel.Info, $"Wallpaper set to {wallpaperPath}");
            }
            else
            {
                _logService.Log(LogLevel.Error, $"Failed to set wallpaper: {Marshal.GetLastWin32Error()}");
            }

            return Task.FromResult(success);
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error setting wallpaper: {ex.Message}");
            return Task.FromResult(false);
        }
    }

}
