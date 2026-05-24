// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Utilities.Managers
{
    public static class RegistryPermissionsManager
    {
        #region Constants

        private const string TrustedInstallerSID = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

        #endregion

        #region Native Token Privileges (P/Invoke)

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        private static void EnablePrivilege(string privilegeName)
        {
            const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
            const uint TOKEN_QUERY = 0x0008;
            const uint SE_PRIVILEGE_ENABLED = 0x00000002;

            IntPtr hProcess = Process.GetCurrentProcess().Handle;
            if (OpenProcessToken(hProcess, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr hToken))
            {
                if (LookupPrivilegeValue(null, privilegeName, out LUID luid))
                {
                    TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES
                    {
                        PrivilegeCount = 1,
                        Luid = luid,
                        Attributes = SE_PRIVILEGE_ENABLED
                    };
                    AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                }
                CloseHandle(hToken);
            }
        }
        #endregion

        #region Ownership Management Methods

        public static bool GrantUltimateAccess(RegistryKey rootHive, string subKeyPath)
        {
            try
            {

                EnablePrivilege("SeTakeOwnershipPrivilege");
                EnablePrivilege("SeRestorePrivilege");

                using (RegistryKey? key = rootHive.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership))
                {
                    if (key == null) return false;
                    RegistrySecurity security = key.GetAccessControl();
                    SecurityIdentifier admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

                    security.SetOwner(admins);
                    key.SetAccessControl(security);
                }

                using (RegistryKey? key = rootHive.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions))
                {
                    if (key == null) return false;
                    RegistrySecurity security = key.GetAccessControl();
                    SecurityIdentifier admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

                    RegistryAccessRule fullControlRule = new RegistryAccessRule(
                        admins, RegistryRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);

                    security.AddAccessRule(fullControlRule);
                    key.SetAccessControl(security);
                }

                Debug.WriteLine($"[Security] Ultimate access granted for: {subKeyPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Security Error] Failed to grant ultimate access: {ex.Message}");
                return false;
            }
        }

        public static bool RestoreTrustedInstallerOwnership(RegistryKey rootHive, string subKeyPath)
        {
            try
            {
                EnablePrivilege("SeTakeOwnershipPrivilege");
                EnablePrivilege("SeRestorePrivilege");

                using (RegistryKey? key = rootHive.OpenSubKey(subKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership | RegistryRights.ChangePermissions))
                {
                    if (key == null) return false;

                    RegistrySecurity security = key.GetAccessControl();

                    SecurityIdentifier tiSid = new SecurityIdentifier(TrustedInstallerSID);

                    security.SetOwner(tiSid);

                    SecurityIdentifier admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                    RegistryAccessRule fullControlRule = new RegistryAccessRule(
                        admins, RegistryRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow);
                    security.RemoveAccessRule(fullControlRule);

                    key.SetAccessControl(security);
                }

                Debug.WriteLine($"[Security] Ownership restored to TrustedInstaller for: {subKeyPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Security Error] Failed to restore ownership: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}