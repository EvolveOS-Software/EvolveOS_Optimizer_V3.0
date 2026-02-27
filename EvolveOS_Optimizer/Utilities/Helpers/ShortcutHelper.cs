using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    internal static class ShortcutHelper
    {
        public const string AppUserModelId = "EvolveOS.Optimizer.App";

        public static void CreateShortcut()
        {
            string shortcutPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
        "EvolveOS Optimizer.lnk");

            if (File.Exists(shortcutPath)) File.Delete(shortcutPath);

            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            try
            {
                IShellLinkW newShortcut = (IShellLinkW)new CShellLink();
                newShortcut.SetPath(exePath);
                newShortcut.SetWorkingDirectory(AppDomain.CurrentDomain.BaseDirectory);

                IPropertyStore propertyStore = (IPropertyStore)newShortcut;
                PropVariant appId = new PropVariant();

                try
                {
                    appId.SetString(AppUserModelId);
                    // System.AppUserModel.ID Key
                    PropertyKey key = new PropertyKey(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
                    propertyStore.SetValue(ref key, ref appId);
                    propertyStore.Commit();
                }
                finally
                {
                    appId.Clear();
                }

                IPersistFile persistFile = (IPersistFile)newShortcut;
                persistFile.Save(shortcutPath, true);

                Win32Helper.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

                Debug.WriteLine("[ShortcutHelper] Shortcut created successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShortcutHelper] Error: {ex.Message}");
            }
        }

        #region COM Interfaces

        [ComImport, Guid("00021401-0000-0000-C000-000000000046"), ClassInterface(ClassInterfaceType.None)]
        private class CShellLink { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([In, MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([In, MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchMaxPath, out int piIcon);
            void SetIconLocation([In, MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([In, MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([In, MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PropertyKey pkey);
            void GetValue(ref PropertyKey pkey, out PropVariant pv);
            void SetValue(ref PropertyKey pkey, ref PropVariant pv);
            void Commit();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropertyKey
        {
            public Guid fmtid;
            public UIntPtr pid;
            public PropertyKey(Guid guid, uint id) { fmtid = guid; pid = (UIntPtr)id; }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PropVariant
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr ptr;

            public void SetString(string value)
            {
                vt = 31;
                ptr = Marshal.StringToCoTaskMemUni(value);
            }

            public void Clear()
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(ptr);
                    ptr = IntPtr.Zero;
                }
                vt = 0;
            }
        }
        #endregion
    }
}