// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using Microsoft.Win32.SafeHandles;
using Vanara.PInvoke;
using static Vanara.PInvoke.AdvApi32;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public static class RegistryServices
    {
        #region Registry Access
        public static Win32Error EVRegOpenKey(HKEY hkey, string subRoot, REGSAM samDesired, out HKEY phkResult, bool use86Arch = false)
        {
            var result = RegOpenKeyEx(hkey, subRoot, 0, samDesired, out var phkRes);

            if (result.Succeeded)
                phkResult = phkRes;
            else
                phkResult = HKEY.NULL;

            return result;
        }
        #endregion

        #region Registry Query Operations
        public static Win32Error GetLastWriteTime(HKEY hkey, string subRoot, out DateTime lastWriteTime)
        {
            lastWriteTime = DateTime.MinValue;

            SafeRegistryHandle handle;

            var result = AdvApi32.RegOpenKeyEx(
                hkey,
                subRoot,
                0,
                AdvApi32.REGSAM.KEY_QUERY_VALUE,
                out handle);

            if (result.Failed)
                return result;

            if (handle == null || handle.IsInvalid)
                return Win32Error.ERROR_INVALID_HANDLE;

            try
            {
                HKEY hkeyHandle = new HKEY(handle.DangerousGetHandle());

                uint classLen = 0;
                System.Runtime.InteropServices.ComTypes.FILETIME ft;

                result = AdvApi32.RegQueryInfoKey(
                    hkeyHandle,
                    null,
                    ref classLen,
                    IntPtr.Zero,
                    out _, out _, out _, out _, out _, out _, out _, out ft);

                if (result.Succeeded)
                {
                    long ticks = (((long)ft.dwHighDateTime) << 32) + (uint)ft.dwLowDateTime;
                    lastWriteTime = DateTime.FromFileTime(ticks);
                }
            }
            finally
            {
                handle.Dispose();
            }

            return result;
        }
        #endregion

        #region Memory & Pointer Utilities
        public unsafe static ref T NullRef<T>()
        {
            return ref Unsafe.AsRef<T>(null);
        }
        #endregion
    }
}