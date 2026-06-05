// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.Native;

namespace EvolveOS_Optimizer.Utilities.Services;

/// <summary>
/// Manages system-level UI refreshes and critical shell processes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Responsibility:</b>
/// This service provides methods to force the Windows shell to immediately recognize 
/// configuration changes (like theme, color, or regional settings) without requiring 
/// a full system reboot or user logoff.
/// </para>
/// <para>
/// <b>Capabilities:</b>
/// It achieves this by broadcasting native Windows messages (<c>WM_SETTINGCHANGE</c>, 
/// <c>WM_SYSCOLORCHANGE</c>, <c>WM_THEMECHANGE</c>) across the system and safely managing the lifecycle 
/// of <c>explorer.exe</c> when a hard shell restart is necessary.
/// </para>
/// </remarks>
public class WindowsUIManagementService : IWindowsUIManagementService
{
    #region Fields & Constructor

    private readonly ILogService _logService;
    private readonly IProcessExecutor _processExecutor;

    public WindowsUIManagementService(ILogService logService, IProcessExecutor processExecutor)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
    }

    #endregion

    #region Process Management

    public bool IsProcessRunning(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            var isRunning = processes.Length > 0;
            foreach (var process in processes)
            {
                process.Dispose();
            }
            return isRunning;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error checking if process {processName} is running", ex);
            return false;
        }
    }

    public void KillProcess(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Failed to kill process {processName}", ex);
        }
    }

    #endregion

    #region GUI & System Broadcasts

    public async Task<OperationResult> RefreshWindowsGUI(bool killExplorer = true)
    {
        try
        {
            IntPtr result;
            User32Api.SendMessageTimeout(
                (IntPtr)User32Api.HWND_BROADCAST, User32Api.WM_SYSCOLORCHANGE,
                IntPtr.Zero, IntPtr.Zero, User32Api.SMTO_ABORTIFHUNG, 1000, out result);
            User32Api.SendMessageTimeout(
                (IntPtr)User32Api.HWND_BROADCAST, User32Api.WM_THEMECHANGE,
                IntPtr.Zero, IntPtr.Zero, User32Api.SMTO_ABORTIFHUNG, 1000, out result);

            if (killExplorer)
            {
                await Task.Delay(500).ConfigureAwait(false);

                bool explorerWasRunning = IsProcessRunning("explorer");

                if (explorerWasRunning)
                {
                    KillProcess("explorer");
                    await Task.Delay(1000).ConfigureAwait(false);

                    int retryCount = 0;
                    const int maxRetries = 5;
                    bool explorerRestarted = false;

                    while (retryCount < maxRetries && !explorerRestarted)
                    {
                        if (IsProcessRunning("explorer"))
                        {
                            explorerRestarted = true;
                        }
                        else
                        {
                            retryCount++;
                            await Task.Delay(1000).ConfigureAwait(false);
                        }
                    }

                    if (!explorerRestarted)
                    {
                        try
                        {
                            await _processExecutor.ShellExecuteAsync("explorer.exe").ConfigureAwait(false);
                            await Task.Delay(2000).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError("Failed to start Explorer manually", ex);
                            return OperationResult.Failed("Failed to start Explorer manually", ex);
                        }
                    }
                }
            }

            string themeChanged = "ImmersiveColorSet";
            IntPtr themeChangedPtr = Marshal.StringToHGlobalUni(themeChanged);

            try
            {
                User32Api.SendMessageTimeout(
                    (IntPtr)User32Api.HWND_BROADCAST, User32Api.WM_SETTINGCHANGE,
                    IntPtr.Zero, themeChangedPtr, User32Api.SMTO_ABORTIFHUNG, 1000, out result);

                User32Api.SendMessageTimeout(
                    (IntPtr)User32Api.HWND_BROADCAST, User32Api.WM_SETTINGCHANGE,
                    IntPtr.Zero, IntPtr.Zero, User32Api.SMTO_ABORTIFHUNG, 1000, out result);
            }
            finally
            {
                Marshal.FreeHGlobal(themeChangedPtr);
            }

            return OperationResult.Succeeded();
        }
        catch (Exception ex)
        {
            _logService.LogError("Error refreshing Windows GUI", ex);
            return OperationResult.Failed("Error refreshing Windows GUI", ex);
        }
    }

    public void BroadcastRegionalSettingChange()
    {
        IntPtr intlPtr = Marshal.StringToHGlobalUni("intl");
        try
        {
            IntPtr result;
            User32Api.SendMessageTimeout(
                (IntPtr)User32Api.HWND_BROADCAST, User32Api.WM_SETTINGCHANGE,
                IntPtr.Zero, intlPtr, User32Api.SMTO_ABORTIFHUNG, 1000, out result);
        }
        finally
        {
            Marshal.FreeHGlobal(intlPtr);
        }
    }

    #endregion
}