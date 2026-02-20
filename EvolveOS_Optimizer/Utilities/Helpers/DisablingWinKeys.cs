using System.Runtime.InteropServices;
using Windows.System;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    internal sealed class DisablingWinKeys : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            internal uint vkCode;
            internal uint scanCode;
            internal uint flags;
            internal uint time;
            internal IntPtr dwExtraInfo;
        }

        internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(int id, LowLevelKeyboardProc callback, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern short GetAsyncKeyState(int vKey);

        private const int WH_KEYBOARD_LL = 13;
        private const int VK_CONTROL = 0x11;

        internal IntPtr ptrHook = IntPtr.Zero;
        internal LowLevelKeyboardProc objKeyboardProcess;

        internal DisablingWinKeys()
        {
            objKeyboardProcess = CaptureKey;
        }

        internal IntPtr CaptureKey(int nCode, IntPtr wp, IntPtr lp)
        {
            if (nCode >= 0)
            {
                KBDLLHOOKSTRUCT objKeyInfo = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lp);
                VirtualKey key = (VirtualKey)objKeyInfo.vkCode;

                bool isWinKey = key == VirtualKey.LeftWindows || key == VirtualKey.RightWindows;

                bool isAltTab = key == VirtualKey.Tab && HasAltModifier(objKeyInfo.flags);

                bool isCtrlEsc = key == VirtualKey.Escape && (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

                bool isAltEsc = key == VirtualKey.Escape && HasAltModifier(objKeyInfo.flags);

                if (isWinKey || isAltTab || isCtrlEsc || isAltEsc)
                {
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(ptrHook, nCode, wp, lp);
        }

        private bool HasAltModifier(uint flags)
        {
            return (flags & 0x20) != 0;
        }

        public void Dispose()
        {
            if (ptrHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(ptrHook);
                ptrHook = IntPtr.Zero;
            }
        }
    }
}