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

public partial class SecurityAdvancedViewModel : ObservableObject
{
    #region Constructor
    public SecurityAdvancedViewModel()
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

    private GridLength _columnType = new(48d);
    public GridLength ColumnType { get => _columnType; set => SetProperty(ref _columnType, value); }

    private GridLength _columnEntity = new(200d);
    public GridLength ColumnEntity { get => _columnEntity; set => SetProperty(ref _columnEntity, value); }

    private GridLength _columnAccess = new(160d);
    public GridLength ColumnAccess { get => _columnAccess; set => SetProperty(ref _columnAccess, value); }

    private GridLength _columnInherited = new(70d);
    public GridLength ColumnInherited { get => _columnInherited; set => SetProperty(ref _columnInherited, value); }

    private bool _isInheritanceDisabled;
    public bool IsInheritanceDisabled
    {
        get => _isInheritanceDisabled;
        set
        {
            if (SetProperty(ref _isInheritanceDisabled, value))
            {
                OnPropertyChanged(nameof(InheritanceButtonText));
            }
        }
    }

    public string InheritanceButtonText => IsInheritanceDisabled ? "Enable inheritance" : "Disable inheritance";
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

            AuthorizationRuleCollection existingExplicitRules = security.GetAccessRules(true, false, typeof(SecurityIdentifier));
            foreach (RegistryAccessRule existingRule in existingExplicitRules)
            {
                security.RemoveAccessRule(existingRule);
            }

            foreach (var principal in Principals)
            {
                if (string.IsNullOrEmpty(principal.Sid) || principal.AccessRuleAdvanced == null)
                    continue;

                if (principal.AccessRuleAdvanced.RawAceFlags.HasFlag(AceFlags.Inherited))
                    continue;

                var sid = new SecurityIdentifier(principal.Sid);
                AccessControlType controlType = principal.AccessRuleAdvanced.RawAceType == AceType.AccessAllowed
                    ? AccessControlType.Allow
                    : AccessControlType.Deny;

                InheritanceFlags inheritance = InheritanceFlags.None;
                PropagationFlags propagation = PropagationFlags.None;

                if (principal.AccessRuleAdvanced.RawAceFlags.HasFlag(AceFlags.ContainerInherit))
                    inheritance |= InheritanceFlags.ContainerInherit;
                if (principal.AccessRuleAdvanced.RawAceFlags.HasFlag(AceFlags.ObjectInherit))
                    inheritance |= InheritanceFlags.ObjectInherit;
                if (principal.AccessRuleAdvanced.RawAceFlags.HasFlag(AceFlags.InheritOnly))
                    propagation |= PropagationFlags.InheritOnly;
                if (principal.AccessRuleAdvanced.RawAceFlags.HasFlag(AceFlags.NoPropagateInherit))
                    propagation |= PropagationFlags.NoPropagateInherit;

                var rule = new RegistryAccessRule(
                    sid,
                    (RegistryRights)principal.AccessRuleAdvanced.RawAccessMask,
                    inheritance,
                    propagation,
                    controlType);

                security.AddAccessRule(rule);
            }

            security.SetAccessRuleProtection(isProtected: IsInheritanceDisabled, preserveInheritance: false);

            targetKey.SetAccessControl(security);

            return Win32Error.ERROR_SUCCESS;
        }
        catch (UnauthorizedAccessException)
        {
            return Win32Error.ERROR_ACCESS_DENIED;
        }
        catch (Exception)
        {
            return Win32Error.ERROR_INTERNAL_ERROR;
        }
    }

    public void LoadKeySecurityOwner()
    {
        if (KeyItem == null) return;

        var result = RegOpenKeyEx(KeyItem.RootHive, KeyItem.Path, 0, REGSAM.READ_CONTROL, out SafeRegistryHandle phKey);
        if (result.Failed) return;

        using (phKey)
        {
            var pSD = new SafePSECURITY_DESCRIPTOR(512);
            var sdsz = (uint)pSD.Size;

            RegGetKeySecurity(phKey, SECURITY_INFORMATION.OWNER_SECURITY_INFORMATION, pSD, ref sdsz);

            bool bResult = GetSecurityDescriptorOwner(pSD, out var lpSid, out var bOwnerDefaulted);

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

            SecurityDescriptorOwner = new()
            {
                SidType = snu,
                Name = lpName.ToString(),
                Domain = lpReferencedDomainName.Length == 0 ? "" : lpReferencedDomainName.ToString().ToLower(),
                Sid = lpSid.ToString(),
            };
        }
    }

    public void GetKeyAccessControlList()
    {
        if (KeyItem == null) return;

        try
        {
            var result = RegOpenKeyEx(KeyItem.RootHive, KeyItem.Path, 0, REGSAM.READ_CONTROL, out SafeRegistryHandle phKey);

            if (result.Failed)
            {
                Kernel32.SetLastError((uint)result);
                return;
            }

            using (phKey)
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

                    GetSecurityDescriptorControl(pSD, out var control, out var revision);
                    IsInheritanceDisabled = control.HasFlag(SECURITY_DESCRIPTOR_CONTROL.SE_DACL_PROTECTED);

                    HasDacl = true;
                    GetKeyAccessControlEntries(pDacl, asi.AceCount);
                }
            }
        }
        catch
        {
        }
    }

    public void RemoveSelectedPrincipal()
    {
        if (SelectedPrincipal == null) return;

        if (SelectedPrincipal.AccessRuleAdvanced != null &&
            SelectedPrincipal.AccessRuleAdvanced.RawAceFlags.HasFlag(AceFlags.Inherited))
        {
            return;
        }

        _principals.Remove(SelectedPrincipal);
    }

    public void ApplyDisableInheritance(bool copyInheritedRules)
    {
        IsInheritanceDisabled = true;

        if (copyInheritedRules)
        {
            foreach (var principal in _principals.Where(p => p.AccessRuleAdvanced?.RawAceFlags.HasFlag(AceFlags.Inherited) == true))
            {
                if (principal.AccessRuleAdvanced != null)
                {
                    principal.AccessRuleAdvanced.RawAceFlags &= ~AceFlags.Inherited;
                    principal.AccessRuleAdvanced.HumanizedIsInheritance = "False";
                    principal.AccessRuleAdvanced.HumanizedAppliesTo = "This key and subkeys";
                }
            }
        }
        else
        {
            var explicitOnly = _principals.Where(p => p.AccessRuleAdvanced?.RawAceFlags.HasFlag(AceFlags.Inherited) == false).ToList();
            _principals.Clear();
            foreach (var p in explicitOnly)
            {
                _principals.Add(p);
            }
        }
    }

    public Win32Error AddNewPrincipal(string accountName)
    {
        try
        {
            var account = new NTAccount(accountName);
            var sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));

            var principal = new PermissionPrincipalItem()
            {
                Sid = sid.Value,
                Name = accountName,
                Domain = "",
                SidType = SID_NAME_USE.SidTypeUser,
                AccessRuleAdvanced = new AccessRuleAdvancedItem()
                {
                    HumanizedAccessControlType = "Allow",
                    HumanizedAccessControl = "Read",
                    HumanizedIsInheritance = "False",
                    HumanizedAppliesTo = "This key and subkeys",
                    RawAccessMask = REGSAM.KEY_READ,
                    RawAceType = AceType.AccessAllowed,
                    RawAceFlags = AceFlags.ContainerInherit
                }
            };

            _principals.Add(principal);
            SelectedPrincipal = principal;

            return Win32Error.ERROR_SUCCESS;
        }
        catch
        {
            return Win32Error.ERROR_NONE_MAPPED;
        }
    }

    public void ApplyEnableInheritance()
    {
        IsInheritanceDisabled = false;
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
                    AccessRuleAdvanced = new()
                    {
                        HumanizedAccessControlType = aceType == AceType.AccessAllowed ? "Allow" : (aceType == AceType.AccessDenied ? "Deny" : "Unknown"),
                        HumanizedAccessControl = "None",
                        HumanizedIsInheritance = isInherited ? "True" : "False",
                        HumanizedAppliesTo = "None",

                        RawAccessMask = (REGSAM)accessMask,
                        RawAceType = aceType,
                        RawAceFlags = aceHeader.AceFlags
                    },
                };

                HumanizeAccessRuleAdvanced(principal.AccessRuleAdvanced, (REGSAM)accessMask);
                HumanizeAppliesTo(principal.AccessRuleAdvanced, aceHeader.AceFlags);

                _principals.Add(principal);

                lpName?.Clear();
                lpReferencedDomainName?.Clear();
            }
        }
        catch
        {
        }
        finally
        {
        }
    }

    private void HumanizeAccessRuleAdvanced(AccessRuleAdvancedItem item, REGSAM mask)
    {
        if (mask.HasFlag(REGSAM.KEY_ALL_ACCESS))
        {
            item.HumanizedAccessControl = "Full Control";
            return;
        }
        else if (mask.HasFlag(REGSAM.KEY_READ))
        {
            item.HumanizedAccessControl = "Read";
            return;
        }

        item.HumanizedAccessControl = "Special";
        return;
    }

    private void HumanizeAppliesTo(AccessRuleAdvancedItem item, AceFlags flags)
    {
        if (flags.HasFlag(AceFlags.ContainerInherit) && flags.HasFlag(AceFlags.InheritOnly))
        {
            item.HumanizedAppliesTo = "Subkeys only";
            return;
        }
        else if (flags.HasFlag(AceFlags.ContainerInherit))
        {
            item.HumanizedAppliesTo = "This key and subkeys";
            return;
        }

        item.HumanizedAppliesTo = "This key only";
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