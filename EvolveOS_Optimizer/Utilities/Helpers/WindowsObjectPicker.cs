// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class WindowsObjectPicker
    {
        public static string? ShowDialog(IntPtr hwnd)
        {
            var picker = (IDsObjectPicker)new DSObjectPicker();

            var scope = new DSOP_SCOPE_INIT_INFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(DSOP_SCOPE_INIT_INFO)),
                flType = 0x000003FF,
                flScope = 0x00000001,
                FilterFlags = new DSOP_FILTER_FLAGS
                {
                    Uplevel = new DSOP_UPLEVEL_FILTER_FLAGS
                    {
                        flBothModes = 0x00000957
                    },
                    flDownlevel = 0x8002000F
                }
            };

            IntPtr pScope = Marshal.AllocHGlobal(Marshal.SizeOf(scope));
            try
            {
                Marshal.StructureToPtr(scope, pScope, false);

                var initInfo = new DSOP_INIT_INFO
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(DSOP_INIT_INFO)),
                    cDsScopeInfos = 1,
                    aDsScopeInfos = pScope,
                    flOptions = 0
                };

                picker.Initialize(ref initInfo);

                if (picker.InvokeDialog(hwnd, out var dataObj) == 0 && dataObj != null)
                {
                    uint cfFormat = RegisterClipboardFormat("CFSTR_DSOP_DS_SELECTION_LIST");
                    var format = new FORMATETC
                    {
                        cfFormat = (short)cfFormat,
                        ptd = IntPtr.Zero,
                        dwAspect = DVASPECT.DVASPECT_CONTENT,
                        lindex = -1,
                        tymed = TYMED.TYMED_HGLOBAL
                    };

                    dataObj.GetData(ref format, out var stg);
                    try
                    {
                        IntPtr pList = GlobalLock(stg.unionmember);
                        if (pList != IntPtr.Zero)
                        {
                            uint cItems = (uint)Marshal.ReadInt32(pList);
                            if (cItems > 0)
                            {
                                IntPtr pSelection = IntPtr.Add(pList, 8);
                                var selection = Marshal.PtrToStructure<DS_SELECTION>(pSelection);

                                return selection.pwzName;
                            }
                            GlobalUnlock(stg.unionmember);
                        }
                    }
                    finally
                    {
                        ReleaseStgMedium(ref stg);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pScope);
            }

            return null;
        }

        #region Native COM Interfaces and Structs

        [ComImport, Guid("17D6CCD8-3B7B-11D2-B9E0-00C04FD8DBF7")]
        private class DSObjectPicker { }

        [ComImport, Guid("0C87E64E-3B7A-11D2-B9E0-00C04FD8DBF7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDsObjectPicker
        {
            void Initialize(ref DSOP_INIT_INFO pInitInfo);
            [PreserveSig]
            int InvokeDialog(IntPtr hwndParent, out IDataObject ppdo);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DSOP_INIT_INFO
        {
            public uint cbSize;
            [MarshalAs(UnmanagedType.LPWStr)] public string? pwzTargetComputer;
            public uint cDsScopeInfos;
            public IntPtr aDsScopeInfos;
            public uint flOptions;
            public uint cAttributesToFetch;
            public IntPtr apwzAttributeNames;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DSOP_SCOPE_INIT_INFO
        {
            public uint cbSize;
            public uint flType;
            public uint flScope;
            public DSOP_FILTER_FLAGS FilterFlags;
            [MarshalAs(UnmanagedType.LPWStr)] public string? pwzDcName;
            [MarshalAs(UnmanagedType.LPWStr)] public string? pwzADsPath;
            public uint hr;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DSOP_FILTER_FLAGS
        {
            public DSOP_UPLEVEL_FILTER_FLAGS Uplevel;
            public uint flDownlevel;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DSOP_UPLEVEL_FILTER_FLAGS
        {
            public uint flBothModes;
            public uint flMixedModeOnly;
            public uint flNativeModeOnly;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DS_SELECTION
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string pwzName;
            [MarshalAs(UnmanagedType.LPWStr)] public string pwzADsPath;
            [MarshalAs(UnmanagedType.LPWStr)] public string pwzClass;
            [MarshalAs(UnmanagedType.LPWStr)] public string pwzUPN;
            public IntPtr pvarFetchedAttributes;
            public uint flScopeType;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterClipboardFormat(string lpszFormat);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GlobalLock(IntPtr handle);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr handle);

        [DllImport("ole32.dll", ExactSpelling = true)]
        private static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);

        #endregion
    }
}