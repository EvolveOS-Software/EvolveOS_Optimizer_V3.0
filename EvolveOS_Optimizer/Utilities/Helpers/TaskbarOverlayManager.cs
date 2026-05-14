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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

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
        private const long WS_CHILD = 0x40000000L;

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

            SetParent(monitorHwnd, taskbarHwnd);

            long style = GetWindowLongPtr(monitorHwnd, GWL_STYLE).ToInt64();

            style &= ~(WS_POPUP | WS_CAPTION | WS_THICKFRAME | WS_BORDER);
            style |= WS_CHILD;

            SetWindowLongPtr(monitorHwnd, GWL_STYLE, new IntPtr(style));

            long exStyle = GetWindowLongPtr(monitorHwnd, GWL_EXSTYLE).ToInt64();
            exStyle |= WS_EX_TOOLWINDOW;
            SetWindowLongPtr(monitorHwnd, GWL_EXSTYLE, new IntPtr(exStyle));

            SetWindowPos(monitorHwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        public static void PositionInsideTaskbar(IntPtr monitorHwnd, int xOffsetFromRight, int yOffsetFromTop)
        {
            var taskbarRect = GetTaskbarRect();
            int relativeX = taskbarRect.Width - xOffsetFromRight;
            SetWindowPos(monitorHwnd, HWND_TOP, relativeX, yOffsetFromTop, 0, 0, SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
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
    }
}