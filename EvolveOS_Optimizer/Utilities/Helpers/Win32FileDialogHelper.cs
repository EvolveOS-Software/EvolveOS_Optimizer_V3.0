// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Utilities.Helpers;

public static class Win32FileDialogHelper
{
    #region COM Interfaces and GUIDs

    private static readonly Guid CLSID_FileOpenDialog = new("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7");
    private static readonly Guid CLSID_FileSaveDialog = new("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B");

    [ComImport]
    [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig] int Show([In] IntPtr parent);

        void SetFileTypes([In] uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] COMDLG_FILTERSPEC[] rgFilterSpec);

        void SetFileTypeIndex([In] uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise([In] IntPtr pfde, out uint pdwCookie);
        void Unadvise([In] uint dwCookie);
        void SetOptions([In] FOS fos);
        void GetOptions(out FOS pfos);
        void SetDefaultFolder([In] IShellItem psi);
        void SetFolder([In] IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace([In] IShellItem psi, int fdap);
        void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close([In] int hr);
        void SetClientGuid([In] ref Guid guid);
        void ClearClientData();
        void SetFilter([In] IntPtr pFilter);
    }

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler([In] IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName([In] SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes([In] uint sfgaoMask, out uint psfgaoAttribs);
        void Compare([In] IShellItem psi, [In] uint hint, out int piOrder);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct COMDLG_FILTERSPEC
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pszName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pszSpec;
    }

    [Flags]
    private enum FOS : uint
    {
        FOS_OVERWRITEPROMPT = 0x2,
        FOS_STRICTFILETYPES = 0x4,
        FOS_NOCHANGEDIR = 0x8,
        FOS_PICKFOLDERS = 0x20,
        FOS_FORCEFILESYSTEM = 0x40,
        FOS_ALLNONSTORAGEITEMS = 0x80,
        FOS_NOVALIDATE = 0x100,
        FOS_ALLOWMULTISELECT = 0x200,
        FOS_PATHMUSTEXIST = 0x800,
        FOS_FILEMUSTEXIST = 0x1000,
        FOS_CREATEPROMPT = 0x2000,
        FOS_SHAREAWARE = 0x4000,
        FOS_NOREADONLYRETURN = 0x8000,
        FOS_NOTESTFILECREATE = 0x10000,
        FOS_HIDEMRUPLACES = 0x20000,
        FOS_HIDEPINNEDPLACES = 0x40000,
        FOS_NODEREFERENCELINKS = 0x100000,
        FOS_DONTADDTORECENT = 0x2000000,
        FOS_FORCESHOWHIDDEN = 0x10000000,
        FOS_DEFAULTNOMINIMODE = 0x20000000,
        FOS_FORCEPREVIEWPANEON = 0x40000000
    }

    private enum SIGDN : uint
    {
        SIGDN_NORMALDISPLAY = 0x00000000,
        SIGDN_PARENTRELATIVEPARSING = 0x80018001,
        SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
        SIGDN_PARENTRELATIVEEDITING = 0x80031001,
        SIGDN_DESKTOPABSOLUTEEDITING = 0x8004c000,
        SIGDN_FILESYSPATH = 0x80058000,
        SIGDN_URL = 0x80068000,
        SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
        SIGDN_PARENTRELATIVE = 0x80080001
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        ref Guid riid,
        out IShellItem ppv);

    #endregion

    public static string? ShowFolderPicker(Window window, string title)
    {
        IFileDialog? dialog = null;
        IShellItem? item = null;
        try
        {
            dialog = (IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_FileOpenDialog)!)!;
            dialog.SetOptions(FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM | FOS.FOS_PATHMUSTEXIST);
            dialog.SetTitle(title);

            var hwnd = WindowNative.GetWindowHandle(window);
            if (dialog.Show(hwnd) != 0) return null;

            dialog.GetResult(out item);
            item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
            return path;
        }
        finally
        {
            if (item != null) Marshal.ReleaseComObject(item);
            if (dialog != null) Marshal.ReleaseComObject(dialog);
        }
    }

    public static string? ShowOpenFilePicker(Window window, string title, string filterName, string filterPattern, string initialFolderPath = "")
    {
        IFileDialog? dialog = null;
        IShellItem? item = null;
        try
        {
            dialog = (IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_FileOpenDialog)!)!;

            var filters = new COMDLG_FILTERSPEC[]
            {
                new() { pszName = filterName, pszSpec = filterPattern },
                new() { pszName = "All Files", pszSpec = "*.*" }
            };

            dialog.SetFileTypes((uint)filters.Length, filters);
            dialog.SetFileTypeIndex(1);
            dialog.SetOptions(FOS.FOS_FORCEFILESYSTEM | FOS.FOS_FILEMUSTEXIST | FOS.FOS_PATHMUSTEXIST);
            dialog.SetTitle(title);

            if (!string.IsNullOrEmpty(initialFolderPath) && Directory.Exists(initialFolderPath))
            {
                SetInitialFolder(dialog, initialFolderPath);
            }

            var hwnd = WindowNative.GetWindowHandle(window);
            if (dialog.Show(hwnd) != 0) return null;

            dialog.GetResult(out item);
            item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
            return path;
        }
        finally
        {
            if (item != null) Marshal.ReleaseComObject(item);
            if (dialog != null) Marshal.ReleaseComObject(dialog);
        }
    }

    public static string? ShowSaveFilePicker(Window window, string title, string filterName, string filterPattern, string defaultFileName, string defaultExtension)
    {
        IFileDialog? dialog = null;
        IShellItem? item = null;
        try
        {
            dialog = (IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_FileSaveDialog)!)!;

            var filters = new COMDLG_FILTERSPEC[]
            {
                new() { pszName = filterName, pszSpec = filterPattern }
            };

            dialog.SetFileTypes((uint)filters.Length, filters);
            dialog.SetFileTypeIndex(1);
            dialog.SetOptions(FOS.FOS_FORCEFILESYSTEM | FOS.FOS_OVERWRITEPROMPT | FOS.FOS_PATHMUSTEXIST);
            dialog.SetTitle(title);
            dialog.SetFileName(defaultFileName);
            dialog.SetDefaultExtension(defaultExtension);

            var hwnd = WindowNative.GetWindowHandle(window);
            if (dialog.Show(hwnd) != 0) return null;

            dialog.GetResult(out item);
            item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
            return path;
        }
        finally
        {
            if (item != null) Marshal.ReleaseComObject(item);
            if (dialog != null) Marshal.ReleaseComObject(dialog);
        }
    }

    private static void SetInitialFolder(IFileDialog dialog, string folderPath)
    {
        var guid = typeof(IShellItem).GUID;
        if (SHCreateItemFromParsingName(folderPath, IntPtr.Zero, ref guid, out var item) == 0)
        {
            dialog.SetFolder(item);
            Marshal.ReleaseComObject(item);
        }
    }
}