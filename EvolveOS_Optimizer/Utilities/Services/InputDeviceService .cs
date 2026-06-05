// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Utilities.Services;

public class InputDeviceService : ISpecialSettingHandler
{
    #region Fields & Constants
    private readonly ILogService _logService;
    private readonly IWindowsRegistryService _registryService;

    private const uint SPI_SETMOUSESPEED = 0x0071;
    private const uint SPI_SETKEYBOARDDELAY = 0x0017;
    private const uint SPI_SETKEYBOARDSPEED = 0x000B;

    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;
    #endregion

    #region Native Methods
    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfo(uint _uiAction, uint _uiParam, uint _pvParam, uint _fWinIni);
    #endregion

    #region Constructor
    public InputDeviceService(ILogService logService, IWindowsRegistryService registryService)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
    }
    #endregion

    #region Interface Implementation
    public async Task<bool> TryApplySpecialSettingAsync(SettingDefinition setting, object value, bool additionalContext = false, ISettingApplicationService? settingApplicationService = null)
    {
        if (setting.Id != "gaming-performance-mouse-sensitivity" &&
            setting.Id != "gaming-performance-keyboard-delay" &&
            setting.Id != "gaming-performance-keyboard-speed")
        {
            return false;
        }

        if (value == null) return false;

        try
        {
            await Task.Run(() =>
            {
                if (double.TryParse(value.ToString(), out double doubleVal))
                {
                    uint numericValue = (uint)Math.Round(doubleVal);
                    string stringValue = numericValue.ToString();

                    switch (setting.Id)
                    {
                        case "gaming-performance-mouse-sensitivity":
                            SystemParametersInfo(SPI_SETMOUSESPEED, 0, numericValue, SPIF_SENDCHANGE | SPIF_UPDATEINIFILE);
                            _registryService.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSensitivity", stringValue, RegistryValueKind.String);
                            break;

                        case "gaming-performance-keyboard-delay":
                            SystemParametersInfo(SPI_SETKEYBOARDDELAY, numericValue, 0, SPIF_SENDCHANGE | SPIF_UPDATEINIFILE);
                            _registryService.SetValue(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "KeyboardDelay", stringValue, RegistryValueKind.String);
                            break;

                        case "gaming-performance-keyboard-speed":
                            SystemParametersInfo(SPI_SETKEYBOARDSPEED, numericValue, 0, SPIF_SENDCHANGE | SPIF_UPDATEINIFILE);
                            _registryService.SetValue(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "KeyboardSpeed", stringValue, RegistryValueKind.String);
                            break;
                    }

                    _logService.Log(LogLevel.Info, $"[InputDeviceService] Successfully applied and saved {setting.Id}: {numericValue}");
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"[InputDeviceService] Failed to apply {setting.Id}: {ex.Message}");
            return false;
        }
    }
    #endregion
}