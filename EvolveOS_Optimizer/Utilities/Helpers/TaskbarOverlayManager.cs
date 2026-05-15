// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class TaskbarOverlayManager
    {
        #region Native Interop
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        private const int GWL_EXSTYLE = -20;
        private const int GWL_STYLE = -16;

        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const long WS_EX_TOPMOST = 0x00000008L;

        private const int GWLP_HWNDPARENT = -8;

        private const long WS_POPUP = 0x80000000L;
        private const long WS_CAPTION = 0x00C00000L;
        private const long WS_THICKFRAME = 0x00040000L;
        private const long WS_BORDER = 0x00800000L;

        private const uint SWP_FRAMECHANGED = 0x0020;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_TOP = new IntPtr(0);

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOACTIVATE = 0x0010;
        #endregion

        #region Universal Helpers
        public static RECT GetTaskbarRect()
        {
            IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
            if (taskbarHwnd != IntPtr.Zero && GetWindowRect(taskbarHwnd, out RECT rect))
            {
                return rect;
            }
            return new RECT { Left = 0, Top = 1040, Right = 1920, Bottom = 1080 };
        }

        public static int GetCurrentWidgetXOffset(IntPtr monitorHwnd)
        {
            GetWindowRect(monitorHwnd, out RECT windowRect);

            var taskbarRect = GetTaskbarRect();

            return taskbarRect.Right - windowRect.Left;
        }

        public static bool AreRectsEqual(RECT a, RECT b)
        {
            return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
        }
        #endregion

        #region Method 1: The "Taskbar Parenting" Approach (Recommended)
        public static void InjectIntoTaskbar(IntPtr monitorHwnd)
        {
            IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
            if (taskbarHwnd == IntPtr.Zero) return;

            SetWindowLongPtr(monitorHwnd, GWLP_HWNDPARENT, taskbarHwnd);

            long style = GetWindowLongPtr(monitorHwnd, GWL_STYLE).ToInt64();
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_BORDER);
            style |= WS_POPUP;
            SetWindowLongPtr(monitorHwnd, GWL_STYLE, new IntPtr(style));

            long exStyle = GetWindowLongPtr(monitorHwnd, GWL_EXSTYLE).ToInt64();
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            SetWindowLongPtr(monitorHwnd, GWL_EXSTYLE, new IntPtr(exStyle));

            GetWindowRect(taskbarHwnd, out RECT taskbarRect);

            int widgetHeight = 36;
            int taskbarHeight = taskbarRect.Bottom - taskbarRect.Top;
            int targetY = taskbarRect.Top + ((taskbarHeight - widgetHeight) / 2);
            int targetX = 500;

            SetWindowPos(monitorHwnd, HWND_TOPMOST, targetX, targetY, 0, 0,
                SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        public static void PositionInsideTaskbar(IntPtr monitorHwnd, int xOffsetFromRight, int yOffsetFromTop)
        {
            var taskbarRect = GetTaskbarRect();

            int absoluteX = taskbarRect.Right - xOffsetFromRight;
            int absoluteY = taskbarRect.Top + yOffsetFromTop;

            SetWindowPos(monitorHwnd, HWND_TOPMOST, absoluteX, absoluteY, 0, 0,
                SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
        }
        #endregion

        #region Method 2: The "Floating Phantom" Approach (Legacy)
        public static void ApplyWidgetStyles(IntPtr monitorHwnd)
        {
            long exStyle = GetWindowLongPtr(monitorHwnd, GWL_EXSTYLE).ToInt64();
            exStyle |= WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            SetWindowLongPtr(monitorHwnd, GWL_EXSTYLE, new IntPtr(exStyle));
        }

        public static void SnapToCoordinates(IntPtr monitorHwnd, int x, int y)
        {
            SetWindowPos(monitorHwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
        }

        public static void EnsureTopmost(IntPtr monitorHwnd)
        {
            SetWindowPos(monitorHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        #endregion

        #region Visibility & Fullscreen Detection
        public static bool ShouldHideWidget()
        {
            IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
            if (taskbarHwnd == IntPtr.Zero) return false;

            IntPtr tbMonitor = MonitorFromWindow(taskbarHwnd, MONITOR_DEFAULTTONEAREST);

            GetWindowRect(taskbarHwnd, out RECT tbRect);
            MONITORINFO miTb = new MONITORINFO();
            miTb.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

            if (GetMonitorInfo(tbMonitor, ref miTb))
            {
                if (tbRect.Top >= miTb.rcMonitor.Bottom - 10)
                {
                    return true;
                }
            }

            IntPtr fgHwnd = GetForegroundWindow();
            if (fgHwnd != IntPtr.Zero)
            {
                IntPtr desktopHwnd = FindWindow("Progman", null);
                IntPtr workerwHwnd = FindWindow("WorkerW", null);

                if (fgHwnd != desktopHwnd && fgHwnd != workerwHwnd)
                {
                    IntPtr fgMonitor = MonitorFromWindow(fgHwnd, MONITOR_DEFAULTTONEAREST);

                    if (fgMonitor == tbMonitor)
                    {
                        GetWindowRect(fgHwnd, out RECT fgRect);

                        if (fgRect.Left <= miTb.rcMonitor.Left &&
                            fgRect.Top <= miTb.rcMonitor.Top &&
                            fgRect.Right >= miTb.rcMonitor.Right &&
                            fgRect.Bottom >= miTb.rcMonitor.Bottom)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        #endregion
    }
}