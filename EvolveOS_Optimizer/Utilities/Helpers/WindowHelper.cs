using System;
using System.Runtime.InteropServices;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class WindowHelper
    {
        private static IntPtr _oldWndProc = IntPtr.Zero;
        private delegate IntPtr WinProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private static WinProc? _newWndProc;

        public static void RegisterMinWidthHeight(IntPtr hWnd, int minWidth, int minHeight)
        {
            _newWndProc = new WinProc((hwnd, msg, wParam, lParam) =>
            {
                if (msg == 0x0024)
                {
                    MINMAXINFO mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    mmi.ptMinTrackSize.X = minWidth;
                    mmi.ptMinTrackSize.Y = minHeight;
                    Marshal.StructureToPtr(mmi, lParam, true);
                }
                return CallWindowProc(_oldWndProc, hwnd, msg, wParam, lParam);
            });

            _oldWndProc = SetWindowLongPtr(hWnd, -4, Marshal.GetFunctionPointerForDelegate(_newWndProc));
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct MINMAXINFO { public POINT ptReserved; public POINT ptMaxSize; public POINT ptMaxPosition; public POINT ptMinTrackSize; public POINT ptMaxTrackSize; }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}
