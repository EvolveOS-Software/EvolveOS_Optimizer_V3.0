// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using EvolveOS_Optimizer.Core.Model;
using Microsoft.Win32.SafeHandles;
using Vanara.PInvoke;
using static Vanara.PInvoke.AdvApi32;

namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class SecurityViewModel : ObservableObject
{
    #region Constructor
    public SecurityViewModel()
    {
        _principals = [];
        Principals = new(_principals);
    }
    #endregion

    #region Fields and Properties
    private KeyItem? _keyItem;
    public KeyItem? KeyItem { get => _keyItem; set => SetProperty(ref _keyItem, value); }

    private readonly ObservableCollection<PermissionPrincipalItem> _principals;
    public ReadOnlyObservableCollection<PermissionPrincipalItem> Principals { get; }

    private PermissionPrincipalItem? _selectedPrincipal;
    public PermissionPrincipalItem? SelectedPrincipal { get => _selectedPrincipal; set => SetProperty(ref _selectedPrincipal, value); }

    private PermissionPrincipalItem? _securityDescriptorOwner;
    public PermissionPrincipalItem? SecurityDescriptorOwner { get => _securityDescriptorOwner; set => SetProperty(ref _securityDescriptorOwner, value); }

    private bool _hasDacl;
    public bool HasDacl { get => _hasDacl; set => SetProperty(ref _hasDacl, value); }
    #endregion

    #region Public Methods
    public Win32Error SaveKeySecurity()
    {
        if (KeyItem == null) return Win32Error.ERROR_INVALID_PARAMETER;

        try
        {
            using var rootKey = GetDotNetRootKey(KeyItem.RootHive);
            if (rootKey == null) return Win32Error.ERROR_INVALID_PARAMETER;

            using var targetKey = string.IsNullOrEmpty(KeyItem.Path)
                ? rootKey
                : rootKey.OpenSubKey(KeyItem.Path,
                    Microsoft.Win32.RegistryKeyPermissionCheck.ReadWriteSubTree,
                    System.Security.AccessControl.RegistryRights.ChangePermissions | System.Security.AccessControl.RegistryRights.ReadPermissions);

            if (targetKey == null) return Win32Error.ERROR_FILE_NOT_FOUND;

            RegistrySecurity security = targetKey.GetAccessControl();
            HashSet<string> purgedSids = new HashSet<string>();

            Debug.WriteLine($"\n--- SAVING SECURITY FOR: {KeyItem.Path} ---");

            foreach (var principal in Principals)
            {
                if (string.IsNullOrEmpty(principal.Sid) || principal.AccessRuleMerged == null)
                    continue;

                var sid = new SecurityIdentifier(principal.Sid);
                var merged = principal.AccessRuleMerged;

                Debug.WriteLine($"Evaluating SID: {principal.Name}");
                Debug.WriteLine($"  UI Checkbox States -> AllowRead: {merged.AllowRead}, AllowFullControl: {merged.AllowFullControl}");

                if (!merged.AllowRead) merged.AllowFullControl = false;
                if (!merged.DenyRead) merged.DenyFullControl = false;

                if (!purgedSids.Contains(principal.Sid))
                {
                    security.PurgeAccessRules(sid);
                    purgedSids.Add(principal.Sid);
                }

                if (!merged.IsInheritedAllowedMask)
                {
                    uint calculatedAllowMask = 0;

                    if (merged.AllowFullControl)
                        calculatedAllowMask |= (uint)RegistryRights.FullControl;
                    else if (merged.AllowRead)
                        calculatedAllowMask |= (uint)RegistryRights.ReadKey;

                    if (merged.AllowSpecialPermissions && merged.MaskAllowed != 0)
                        calculatedAllowMask |= ((uint)merged.MaskAllowed & ~(uint)RegistryRights.FullControl & ~(uint)RegistryRights.ReadKey);

                    if (calculatedAllowMask != 0)
                    {
                        Debug.WriteLine($"  Adding Explicit ALLOW Mask: {calculatedAllowMask}");
                        var rule = new RegistryAccessRule(
                            sid,
                            (RegistryRights)calculatedAllowMask,
                            InheritanceFlags.ContainerInherit,
                            PropagationFlags.None,
                            AccessControlType.Allow);

                        security.AddAccessRule(rule);
                    }
                }

                if (!merged.IsInheritedDeniedMask)
                {
                    uint calculatedDenyMask = 0;

                    if (merged.DenyFullControl)
                        calculatedDenyMask |= (uint)RegistryRights.FullControl;
                    else if (merged.DenyRead)
                        calculatedDenyMask |= (uint)RegistryRights.ReadKey;

                    if (merged.DenySpecialPermissions && merged.MaskDenied != 0)
                        calculatedDenyMask |= ((uint)merged.MaskDenied & ~(uint)RegistryRights.FullControl & ~(uint)RegistryRights.ReadKey);

                    if (calculatedDenyMask != 0)
                    {
                        Debug.WriteLine($"  Adding Explicit DENY Mask: {calculatedDenyMask}");
                        var rule = new RegistryAccessRule(
                            sid,
                            (RegistryRights)calculatedDenyMask,
                            InheritanceFlags.ContainerInherit,
                            PropagationFlags.None,
                            AccessControlType.Deny);

                        security.AddAccessRule(rule);
                    }
                }
            }

            targetKey.SetAccessControl(security);

            Debug.WriteLine("--- SAVE COMPLETE ---\n");

            return Win32Error.ERROR_SUCCESS;
        }
        catch (UnauthorizedAccessException)
        {
            Debug.WriteLine("SAVE ERROR: UnauthorizedAccessException. Your app must be run as Administrator to change permissions!");
            return Win32Error.ERROR_ACCESS_DENIED;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SAVE ERROR: {ex.Message}");
            return Win32Error.ERROR_INTERNAL_ERROR;
        }
    }

    public void GetKeyAccessControlList()
    {
        if (KeyItem == null) return;

        var result = RegOpenKeyEx(
            KeyItem.RootHive,
            KeyItem.Path,
            0,
            REGSAM.READ_CONTROL,
            out SafeRegistryHandle phKey);

        if (result.Failed)
        {
            Kernel32.SetLastError((uint)result);
            return;
        }

        using (phKey)
        {
            try
            {
                var pSD = new SafePSECURITY_DESCRIPTOR(1024);
                var cbSD = (uint)pSD.Size;

                result = RegGetKeySecurity(phKey, SECURITY_INFORMATION.DACL_SECURITY_INFORMATION, pSD, ref cbSD);
                if (result.Failed)
                {
                    Kernel32.SetLastError((uint)result);
                    return;
                }

                bool bResult = GetSecurityDescriptorDacl(pSD, out var lpbDaclPresent, out var pDacl, out bool lpbDaclDefaulted);

                if (!bResult)
                {
                    result = Kernel32.GetLastError();
                    Kernel32.SetLastError((uint)result);
                    return;
                }

                if (!lpbDaclPresent)
                {
                    HasDacl = false;
                    EnsureDefaultPrincipals();
                    return;
                }
                else
                {
                    bResult = GetAclInformation(pDacl, out ACL_SIZE_INFORMATION asi, (uint)Marshal.SizeOf(typeof(ACL_SIZE_INFORMATION)), ACL_INFORMATION_CLASS.AclSizeInformation);

                    if (!bResult)
                    {
                        result = Kernel32.GetLastError();
                        Kernel32.SetLastError((uint)result);
                        return;
                    }

                    HasDacl = true;

                    GetKeyAccessControlEntries(pDacl, asi.AceCount);
                    EnsureDefaultPrincipals();
                }
            }
            catch
            {
                // silent
            }
        }
    }
    #endregion

    #region Private Methods
    private void GetKeyAccessControlEntries(PACL pDacl, uint nAceCount)
    {
        Win32Error result;
        bool bResult;

        SafePSID lpSid;
        AceType aceType;
        ACE_HEADER aceHeader;
        bool aceIsObjectAce;
        uint accessMask;

        try
        {
            for (var index = 0U; index < nAceCount; index++)
            {
                bResult = GetAce(pDacl, index, out var pAce);
                result = Kernel32.GetLastError();

                lpSid = pAce.GetSid();
                aceType = pAce.GetAceType();
                aceHeader = pAce.GetHeader();
                aceIsObjectAce = pAce.IsObjectAce();
                accessMask = pAce.GetMask();

                bool isInherited = aceHeader.AceFlags.HasFlag(AceFlags.Inherited);

                var cchName = 2048;
                var cchReferencedDomainName = 2048;
                var lpName = new StringBuilder(cchName, cchName);
                var lpReferencedDomainName = new StringBuilder(cchReferencedDomainName, cchReferencedDomainName);

                bResult = LookupAccountSid(null, lpSid, lpName, ref cchName, lpReferencedDomainName, ref cchReferencedDomainName, out var snu);
                result = Kernel32.GetLastError();

                if (result.Failed && result == Win32Error.ERROR_NONE_MAPPED)
                {
                    lpName.Clear();
                    lpReferencedDomainName.Clear();
                }

                if (lpReferencedDomainName.ToString() == "BUILTIN")
                {
                    lpReferencedDomainName.Clear();
                    lpReferencedDomainName = new(256, 256);
                    uint size = (uint)lpReferencedDomainName.Capacity;
                    bResult = Kernel32.GetComputerName(lpReferencedDomainName, ref size);
                }
                else
                {
                    lpReferencedDomainName.Clear();
                }

                var principal = new PermissionPrincipalItem()
                {
                    SidType = snu,
                    Sid = pAce.GetSid().ToString(),
                    Name = lpName.ToString(),
                    Domain = lpReferencedDomainName.ToString().ToLower(),
                };

                uint fullControlMask = (uint)RegistryRights.FullControl;
                uint readKeyMask = (uint)RegistryRights.ReadKey;

                bool isFullControl = (accessMask & fullControlMask) == fullControlMask;
                bool isRead = (accessMask & readKeyMask) == readKeyMask;
                bool isSpecial = !isFullControl && !isRead && accessMask != 0;

                if (aceType == AceType.AccessAllowed)
                {
                    principal.AccessRuleMerged = new()
                    {
                        MaskAllowed = (REGSAM)accessMask,

                        IsInheritedAllowedMask = isInherited,
                        IsInheritedDeniedMask = false,

                        AllowFullControl = isFullControl,
                        AllowRead = isRead || isFullControl,
                        AllowSpecialPermissions = isSpecial
                    };
                }
                else if (aceType == AceType.AccessDenied)
                {
                    principal.AccessRuleMerged = new()
                    {
                        MaskDenied = (REGSAM)accessMask,

                        IsInheritedAllowedMask = false,
                        IsInheritedDeniedMask = isInherited,

                        DenyFullControl = isFullControl,
                        DenyRead = isRead || isFullControl,
                        DenySpecialPermissions = isSpecial
                    };
                }

                _principals.Add(principal);

                lpName?.Clear();
                lpReferencedDomainName?.Clear();
            }
        }
        catch
        {
        }
    }

    private void EnsureDefaultPrincipals()
    {
        var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value;

        if (!_principals.Any(p => p.Sid == adminSid))
        {
            _principals.Add(new PermissionPrincipalItem()
            {
                Sid = adminSid,
                Name = "Administrators",
                Domain = "builtin",
                SidType = AdvApi32.SID_NAME_USE.SidTypeAlias,
                AccessRuleMerged = new AccessRuleMergedItem()
            });
        }

        var currentUserSid = WindowsIdentity.GetCurrent().User?.Value;

        if (currentUserSid != null && !_principals.Any(p => p.Sid == currentUserSid))
        {
            _principals.Add(new PermissionPrincipalItem()
            {
                Sid = currentUserSid,
                Name = Environment.UserName,
                Domain = Environment.UserDomainName.ToLower(),
                SidType = AdvApi32.SID_NAME_USE.SidTypeUser,
                AccessRuleMerged = new AccessRuleMergedItem()
            });
        }
    }

    private Microsoft.Win32.RegistryKey? GetDotNetRootKey(HKEY hkey)
    {
        var view = Environment.Is64BitOperatingSystem ? Microsoft.Win32.RegistryView.Registry64 : Microsoft.Win32.RegistryView.Default;

        if (hkey == HKEY.HKEY_CLASSES_ROOT) return Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.ClassesRoot, view);
        if (hkey == HKEY.HKEY_CURRENT_USER) return Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser, view);
        if (hkey == HKEY.HKEY_LOCAL_MACHINE) return Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, view);
        if (hkey == HKEY.HKEY_USERS) return Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.Users, view);
        if (hkey == HKEY.HKEY_CURRENT_CONFIG) return Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentConfig, view);

        return null;
    }
    #endregion
}