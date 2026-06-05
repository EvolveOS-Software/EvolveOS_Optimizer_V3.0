// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace EvolveOS_Optimizer.Core.Native;

public static class User32Api
{
    public const int HWND_BROADCAST = 0xffff;
    public const uint WM_SYSCOLORCHANGE = 0x0015;
    public const uint WM_SETTINGCHANGE = 0x001A;
    public const uint WM_THEMECHANGE = 0x031A;

    public const uint SMTO_ABORTIFHUNG = 0x0002;

    public const int SW_RESTORE = 9;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int SystemParametersInfo(int uAction, int uParam, string? lpvParam, int fuWinIni);
}
