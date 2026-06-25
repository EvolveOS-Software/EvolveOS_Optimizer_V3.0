// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Utilities.Services;

/// <summary>
/// Provides advanced, context-aware interactions with the Windows Registry.
/// </summary>
/// <remarks>
/// <para>
/// <b>Responsibility:</b>
/// This service abstracts <c>Microsoft.Win32.Registry</c> to provide high-level application of 
/// configuration states. It goes beyond simple read/write operations by handling complex data 
/// structures like Binary BitMasks, Composite Strings (semicolon-separated key-value pairs), 
/// and Batch queries for performance optimization.
/// </para>
/// <para>
/// <b>Capabilities:</b>
/// <list type="bullet">
/// <item><b>Context Awareness (OTS):</b> Automatically intercepts <c>HKCU</c> requests and redirects them to the true interactive user's <c>HKU\{SID}</c> when running in Over-The-Shoulder elevation mode.</item>
/// <item><b>Permission Locking:</b> Can lock registry keys down to read-only for SYSTEM, preventing Windows from reverting specific optimization settings.</item>
/// <item><b>Safe Deletion:</b> Includes guardrails against deleting critical system root keys or shallow paths to prevent catastrophic OS corruption.</item>
/// </list>
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public class WindowsRegistryService(ILogService logService, IInteractiveUserService interactiveUserService) : IWindowsRegistryService
{
    #region Static Helpers & Constants

    private static object? GetWriteValue(object?[]? values) => values?.FirstOrDefault(v => v != null);

    private static object? GetParentDisableValue(object?[]? disabledValues) =>
        disabledValues?.Length > 1 ? disabledValues[1] : GetWriteValue(disabledValues);

    private const int MinDeleteDepth = 2;

    internal static readonly HashSet<string> ProtectedSubKeyRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        @"SOFTWARE\Microsoft\Windows",
        @"SOFTWARE\Microsoft\Windows NT",
        @"SOFTWARE\Policies",
        @"SYSTEM\CurrentControlSet",
        @"SYSTEM\CurrentControlSet\Services",
    };

    #endregion

    #region Basic Operations (Create, Read, Write, Delete)

    private bool CreateKey(string keyPath)
    {
        try
        {
            if (KeyExists(keyPath))
                return true;

            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var createdKey = rootKey.CreateSubKey(subKeyPath, true);
            return createdKey != null;
        }
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to create key '{keyPath}': {ex.Message}");
            return false;
        }
    }

    public bool SetValue(
        string keyPath,
        string valueName,
        object value,
        RegistryValueKind valueKind
    )
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var targetKey = rootKey.CreateSubKey(subKeyPath, true);
            if (targetKey == null)
                return false;

            targetKey.SetValue(valueName, value, valueKind);
            return true;
        }
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to set value '{keyPath}\\{valueName}': {ex.Message}");
            return false;
        }
    }

    public object? GetValue(string keyPath, string valueName)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(subKeyPath, false);
            return key?.GetValue(valueName);
        }
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to get value '{keyPath}\\{valueName}': {ex.Message}");
            return null;
        }
    }

    public bool DeleteKey(string keyPath)
    {
        try
        {
            if (!KeyExists(keyPath))
                return true;

            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);

            var segments = subKeyPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < MinDeleteDepth)
            {
                logService.Log(LogLevel.Warning,
                    $"[WindowsRegistryService] Refusing to delete shallow registry key '{keyPath}' (depth {segments.Length} < {MinDeleteDepth})");
                return false;
            }

            foreach (var protectedRoot in ProtectedSubKeyRoots)
            {
                if (subKeyPath.Equals(protectedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    logService.Log(LogLevel.Warning,
                        $"[WindowsRegistryService] Refusing to delete protected registry key '{keyPath}'");
                    return false;
                }
            }

            rootKey.DeleteSubKeyTree(subKeyPath, false);
            return true;
        }
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to delete key '{keyPath}': {ex.Message}");
            return false;
        }
    }

    public bool DeleteValue(string keyPath, string valueName)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(subKeyPath, true);
            if (key == null)
                return false;

            key.DeleteValue(valueName, false);
            return true;
        }
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to delete value '{keyPath}\\{valueName}': {ex.Message}");
            return false;
        }
    }

    public bool KeyExists(string keyPath)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(subKeyPath, false);
            return key != null;
        }
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to check key existence '{keyPath}': {ex.Message}");
            return false;
        }
    }

    public bool ValueExists(string keyPath, string valueName)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(subKeyPath, false);
            if (key == null)
                return false;

            return key.GetValueNames().Contains(valueName, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to check value existence '{keyPath}\\{valueName}': {ex.Message}");
            return false;
        }
    }

    public string[] GetSubKeyNames(string keyPath)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(subKeyPath, false);
            return key?.GetSubKeyNames() ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to get subkey names for '{keyPath}': {ex.Message}");
            return Array.Empty<string>();
        }
    }

    #endregion

    #region State Evaluation

    public bool IsRegistryValueInEnabledState(RegistrySetting setting, object? currentValue, bool valueExists)
    {
        if (setting == null)
            return false;

        if (setting.ValueName == null && currentValue is bool keyExists)
        {
            if (setting.EnabledValue?.Contains(null) != true
                && setting.DisabledValue?.Contains(null) == true)
            {
                return !keyExists;
            }
            return keyExists;
        }

        if (setting.CompositeStringKey != null)
        {
            var compositeStr = currentValue?.ToString() ?? "";
            var pairs = ParseCompositeString(compositeStr);

            if (pairs.TryGetValue(setting.CompositeStringKey, out var subValue))
            {
                var enabledStr = GetWriteValue(setting.EnabledValue)?.ToString();
                return string.Equals(subValue, enabledStr, StringComparison.OrdinalIgnoreCase);
            }

            var defaultStr = setting.DefaultValue?.ToString();
            var enabledStrFallback = GetWriteValue(setting.EnabledValue)?.ToString();
            return string.Equals(defaultStr, enabledStrFallback, StringComparison.OrdinalIgnoreCase);
        }

        if (setting.EnabledValue == null)
        {
            if (!valueExists)
                return false;
            if (setting.DisabledValue != null && setting.DisabledValue.Any(dv => dv != null && CompareValues(currentValue, dv)))
                return false;
            if (currentValue is false)
                return false;
            return true;
        }

        if (!valueExists)
            return setting.EnabledValue.Contains(null);

        foreach (var ev in setting.EnabledValue)
        {
            if (ev != null && CompareValues(currentValue, ev))
                return true;
        }

        if (setting.DisabledValue != null && setting.DisabledValue.Any(dv => dv != null && CompareValues(currentValue, dv)))
            return false;

        return false;
    }

    public bool IsSettingApplied(RegistrySetting setting)
    {
        try
        {
            if (setting == null)
                return false;

            if (setting.ApplyPerNetworkInterface)
            {
                var subKeys = GetSubKeyNames(setting.KeyPath);
                if (subKeys.Length == 0)
                    return false;

                foreach (var subKey in subKeys)
                {
                    var expandedSetting = setting with
                    {
                        KeyPath = $@"{setting.KeyPath}\{subKey}",
                        ApplyPerNetworkInterface = false
                    };
                    if (!IsSettingApplied(expandedSetting))
                        return false;
                }
                return true;
            }

            if (setting.ApplyPerMonitor)
            {
                var subKeys = GetSubKeyNames(setting.KeyPath);
                if (subKeys.Length == 0)
                    return false;

                foreach (var subKey in subKeys)
                {
                    var expandedSetting = setting with
                    {
                        KeyPath = $@"{setting.KeyPath}\{subKey}",
                        ApplyPerMonitor = false
                    };
                    if (!IsSettingApplied(expandedSetting))
                        return false;
                }
                return true;
            }

            if (setting.ValueName == null && setting.EnabledValue == null && setting.DisabledValue == null)
            {
                return KeyExists(setting.KeyPath);
            }

            if (!KeyExists(setting.KeyPath))
            {
                if (setting.CompositeStringKey != null)
                    return IsRegistryValueInEnabledState(setting, null, false);
                return setting.EnabledValue == null;
            }

            if (setting.CompositeStringKey != null)
            {
                var compositeStr = ValueExists(setting.KeyPath, setting.ValueName!)
                    ? (GetValue(setting.KeyPath, setting.ValueName!)?.ToString() ?? "")
                    : "";
                return IsRegistryValueInEnabledState(setting, compositeStr, !string.IsNullOrEmpty(compositeStr));
            }

            if (!ValueExists(setting.KeyPath, setting.ValueName!))
            {
                return IsRegistryValueInEnabledState(setting, null, false);
            }

            if (setting.BitMask.HasValue && setting.BinaryByteIndex.HasValue)
            {
                return IsBitSet(setting.KeyPath, setting.ValueName!, setting.BinaryByteIndex.Value, setting.BitMask.Value);
            }

            if (setting.ModifyByteOnly && setting.BinaryByteIndex.HasValue)
            {
                var currentByte = GetBinaryByte(setting.KeyPath, setting.ValueName!, setting.BinaryByteIndex.Value);
                if (currentByte == null)
                    return false;

                var enabledByte = GetWriteValue(setting.EnabledValue) switch
                {
                    byte b => b,
                    int i => (byte)i,
                    _ => (byte)0
                };

                return currentByte.Value == enabledByte;
            }

            var currentValue = GetValue(setting.KeyPath, setting.ValueName!);
            return IsRegistryValueInEnabledState(setting, currentValue, true);
        }
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to check if setting applied '{setting?.KeyPath}\\{setting?.ValueName}': {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Binary & BitMask Operations

    private bool ModifyBinaryByte(string keyPath, string valueName, int byteIndex, byte newValue)
    {
        try
        {
            var currentValue = GetValue(keyPath, valueName);
            if (currentValue is not byte[] currentBytes)
            {
                var defaultBinary = new byte[Math.Max(12, byteIndex + 1)];
                defaultBinary[byteIndex] = newValue;
                return SetValue(keyPath, valueName, defaultBinary, RegistryValueKind.Binary);
            }

            if (currentBytes.Length <= byteIndex)
            {
                var expandedBytes = new byte[byteIndex + 1];
                Array.Copy(currentBytes, expandedBytes, currentBytes.Length);
                expandedBytes[byteIndex] = newValue;
                return SetValue(keyPath, valueName, expandedBytes, RegistryValueKind.Binary);
            }

            var modifiedBytes = (byte[])currentBytes.Clone();
            modifiedBytes[byteIndex] = newValue;

            return SetValue(keyPath, valueName, modifiedBytes, RegistryValueKind.Binary);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[WindowsRegistryService] Error modifying byte at index {byteIndex} in '{keyPath}\\{valueName}': {ex.Message}");
            return false;
        }
    }

    private byte? GetBinaryByte(string keyPath, string valueName, int byteIndex)
    {
        try
        {
            var currentValue = GetValue(keyPath, valueName);
            if (currentValue is byte[] currentBytes && currentBytes.Length > byteIndex)
            {
                return currentBytes[byteIndex];
            }
            return null;
        }
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to get binary byte at index {byteIndex} in '{keyPath}\\{valueName}': {ex.Message}");
            return null;
        }
    }

    private bool ModifyBinaryBit(string keyPath, string valueName, int byteIndex, byte bitMask, bool setBit)
    {
        try
        {
            var currentValue = GetValue(keyPath, valueName);
            if (currentValue is not byte[] currentBytes)
            {
                var defaultBinary = new byte[Math.Max(12, byteIndex + 1)];
                defaultBinary[byteIndex] = setBit ? bitMask : (byte)0;
                return SetValue(keyPath, valueName, defaultBinary, RegistryValueKind.Binary);
            }

            if (currentBytes.Length <= byteIndex)
            {
                var expandedBytes = new byte[byteIndex + 1];
                Array.Copy(currentBytes, expandedBytes, currentBytes.Length);
                expandedBytes[byteIndex] = setBit ? bitMask : (byte)0;
                return SetValue(keyPath, valueName, expandedBytes, RegistryValueKind.Binary);
            }

            var modifiedBytes = (byte[])currentBytes.Clone();
            if (setBit)
                modifiedBytes[byteIndex] |= bitMask;
            else
                modifiedBytes[byteIndex] &= (byte)~bitMask;

            return SetValue(keyPath, valueName, modifiedBytes, RegistryValueKind.Binary);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[WindowsRegistryService] Error modifying bit mask 0x{bitMask:X2} at byte index {byteIndex} in '{keyPath}\\{valueName}': {ex.Message}");
            return false;
        }
    }

    private bool IsBitSet(string keyPath, string valueName, int byteIndex, byte bitMask)
    {
        try
        {
            var currentByte = GetBinaryByte(keyPath, valueName, byteIndex);
            if (!currentByte.HasValue)
                return false;

            return (currentByte.Value & bitMask) == bitMask;
        }
        catch (Exception ex)
        {
            logService.LogDebug($"[WindowsRegistryService] Failed to check bit in '{keyPath}\\{valueName}': {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Setting Application

    public bool ApplySetting(RegistrySetting setting, bool isEnabled, object? specificValue = null, bool useDefaultValue = false)
    {
        if (setting == null)
            return false;

        try
        {
            if (setting.ApplyPerNetworkInterface)
            {
                var subKeys = GetSubKeyNames(setting.KeyPath);
                if (subKeys.Length == 0)
                {
                    logService.Log(LogLevel.Warning, $"[WindowsRegistryService] No subkeys found under '{setting.KeyPath}' for per-interface setting");
                    return false;
                }

                var allSucceeded = true;
                foreach (var subKey in subKeys)
                {
                    var expandedSetting = setting with
                    {
                        KeyPath = $@"{setting.KeyPath}\{subKey}",
                        ApplyPerNetworkInterface = false
                    };
                    if (!ApplySetting(expandedSetting, isEnabled, specificValue))
                        allSucceeded = false;
                }
                return allSucceeded;
            }

            if (setting.ApplyPerMonitor)
            {
                var subKeys = GetSubKeyNames(setting.KeyPath);
                if (subKeys.Length == 0)
                {
                    logService.Log(LogLevel.Warning, $"[WindowsRegistryService] No subkeys found under '{setting.KeyPath}' for per-monitor setting");
                    return false;
                }

                var allSucceeded = true;
                foreach (var subKey in subKeys)
                {
                    var expandedSetting = setting with
                    {
                        KeyPath = $@"{setting.KeyPath}\{subKey}",
                        ApplyPerMonitor = false
                    };
                    if (!ApplySetting(expandedSetting, isEnabled, specificValue))
                        allSucceeded = false;
                }
                return allSucceeded;
            }

            logService.Log(LogLevel.Info, $"[WindowsRegistryService] Applying registry setting - Path: {setting.KeyPath}, Value: {setting.ValueName}, Enabled: {isEnabled}");

            if (setting.ValueName == null)
            {
                var result = isEnabled ? CreateKey(setting.KeyPath) : DeleteKey(setting.KeyPath);
                return result;
            }

            if (setting.CompositeStringKey != null)
            {
                if (!CreateKey(setting.KeyPath))
                    return false;

                var currentComposite = ValueExists(setting.KeyPath, setting.ValueName)
                    ? (GetValue(setting.KeyPath, setting.ValueName)?.ToString() ?? "")
                    : "";

                var pairs = ParseCompositeString(currentComposite);
                var newSubValue = specificValue?.ToString()
                    ?? (isEnabled ? GetWriteValue(setting.EnabledValue)?.ToString() : GetWriteValue(setting.DisabledValue)?.ToString());

                if (newSubValue != null)
                    pairs[setting.CompositeStringKey] = newSubValue;
                else
                    pairs.Remove(setting.CompositeStringKey);

                var mergedValue = BuildCompositeString(pairs);
                var compositeResult = SetValue(setting.KeyPath, setting.ValueName, mergedValue, RegistryValueKind.String);

                logService.Log(LogLevel.Info,
                    $"[WindowsRegistryService] Updated composite key '{setting.CompositeStringKey}' to '{newSubValue}' in '{setting.KeyPath}\\{setting.ValueName}' - Full value: '{mergedValue}' - Success: {compositeResult}");
                return compositeResult;
            }

            if (setting.BitMask.HasValue && setting.BinaryByteIndex.HasValue)
            {
                if (!CreateKey(setting.KeyPath))
                    return false;

                var setBit = specificValue switch
                {
                    bool b => b,
                    int i => i != 0,
                    byte b => b != 0,
                    _ => isEnabled
                };
                var result = ModifyBinaryBit(setting.KeyPath, setting.ValueName, setting.BinaryByteIndex.Value, setting.BitMask.Value, setBit);
                logService.Log(LogLevel.Info, $"[WindowsRegistryService] Modified bit mask 0x{setting.BitMask.Value:X2} at byte index {setting.BinaryByteIndex.Value} to {setBit} - Success: {result}");
                return result;
            }

            if (setting.ModifyByteOnly && setting.BinaryByteIndex.HasValue)
            {
                var byteValue = specificValue switch
                {
                    byte b => b,
                    int i => (byte)i,
                    _ when isEnabled => GetWriteValue(setting.EnabledValue) switch
                    {
                        byte b => b,
                        int i => (byte)i,
                        _ => (byte)0
                    },
                    _ => GetWriteValue(setting.DisabledValue) switch
                    {
                        byte b => b,
                        int i => (byte)i,
                        _ => (byte)0
                    }
                };

                if (!CreateKey(setting.KeyPath))
                    return false;

                var result = ModifyBinaryByte(setting.KeyPath, setting.ValueName, setting.BinaryByteIndex.Value, byteValue);
                logService.Log(LogLevel.Info, $"[WindowsRegistryService] Modified byte at index {setting.BinaryByteIndex.Value} to {byteValue:X2} - Success: {result}");
                return result;
            }

            if (setting.LockKeyAccess)
            {
                UnlockRegistryKey(setting.KeyPath);
            }

            var oldValue = GetValue(setting.KeyPath, setting.ValueName);
            var valueToSet = useDefaultValue
                ? GetParentDisableValue(setting.DisabledValue)
                : specificValue ?? (isEnabled
                    ? GetWriteValue(setting.EnabledValue)
                    : GetWriteValue(setting.DisabledValue));

            logService.Log(LogLevel.Info, $"[WindowsRegistryService] Setting '{setting.KeyPath}\\{setting.ValueName}' - Old: {oldValue}, New: {valueToSet}{(useDefaultValue ? " (parent cascade disable)" : "")}");

            if (valueToSet == null)
            {
                var result = DeleteValue(setting.KeyPath, setting.ValueName);
                logService.Log(LogLevel.Info, $"[WindowsRegistryService] Deleted value '{setting.ValueName}' from '{setting.KeyPath}' - Success: {result}");
                return result;
            }

            if (!CreateKey(setting.KeyPath))
                return false;

            var setResult = SetValue(setting.KeyPath, setting.ValueName, valueToSet, setting.ValueType);

            logService.Log(LogLevel.Info, $"[WindowsRegistryService] Set value '{setting.ValueName}' = '{valueToSet}' in '{setting.KeyPath}' - Success: {setResult}");

            if (setResult && setting.LockKeyAccess)
            {
                var writtenValue = useDefaultValue
                    ? GetParentDisableValue(setting.DisabledValue)
                    : specificValue ?? (isEnabled
                        ? GetWriteValue(setting.EnabledValue)
                        : GetWriteValue(setting.DisabledValue));

                if (!isEnabled || (writtenValue is int intVal && intVal == 4))
                {
                    LockRegistryKey(setting.KeyPath);
                }
            }

            return setResult;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[WindowsRegistryService] Error applying setting '{setting.KeyPath}\\{setting.ValueName}': {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Permissions & Locking

    private bool LockRegistryKey(string keyPath)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(
                subKeyPath,
                RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.ChangePermissions | RegistryRights.ReadKey | RegistryRights.TakeOwnership);

            if (key == null)
            {
                logService.Log(LogLevel.Warning, $"[WindowsRegistryService] Cannot lock key '{keyPath}': key not found");
                return false;
            }

            var security = key.GetAccessControl();

            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.SetOwner(adminsSid);

            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            foreach (RegistryAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                security.RemoveAccessRule(rule);
            }

            security.AddAccessRule(new RegistryAccessRule(
                adminsSid,
                RegistryRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            security.AddAccessRule(new RegistryAccessRule(
                systemSid,
                RegistryRights.ReadKey,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            key.SetAccessControl(security);

            logService.Log(LogLevel.Info, $"[WindowsRegistryService] Locked registry key '{keyPath}' to read-only for SYSTEM");
            return true;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[WindowsRegistryService] Failed to lock registry key '{keyPath}': {ex.Message}");
            return false;
        }
    }

    private bool UnlockRegistryKey(string keyPath)
    {
        try
        {
            var (rootKey, subKeyPath) = ParseKeyPath(keyPath);
            using var key = rootKey.OpenSubKey(
                subKeyPath,
                RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.ChangePermissions | RegistryRights.ReadKey | RegistryRights.TakeOwnership);

            if (key == null)
            {
                logService.Log(LogLevel.Warning, $"[WindowsRegistryService] Cannot unlock key '{keyPath}': key not found");
                return false;
            }

            var security = key.GetAccessControl();

            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.SetOwner(adminsSid);

            foreach (RegistryAccessRule rule in security.GetAccessRules(true, false, typeof(SecurityIdentifier)))
            {
                security.RemoveAccessRule(rule);
            }

            security.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);

            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            security.AddAccessRule(new RegistryAccessRule(
                systemSid,
                RegistryRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            security.AddAccessRule(new RegistryAccessRule(
                adminsSid,
                RegistryRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            key.SetAccessControl(security);

            logService.Log(LogLevel.Info, $"[WindowsRegistryService] Unlocked registry key '{keyPath}' - restored SYSTEM full control");
            return true;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[WindowsRegistryService] Failed to unlock registry key '{keyPath}': {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Composite Strings

    private static Dictionary<string, string> ParseCompositeString(string value)
    {
        var pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(value))
            return pairs;

        foreach (var entry in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eqIndex = entry.IndexOf('=');
            if (eqIndex > 0)
                pairs[entry[..eqIndex]] = entry[(eqIndex + 1)..];
        }
        return pairs;
    }

    private static string BuildCompositeString(Dictionary<string, string> pairs)
    {
        if (pairs.Count == 0)
            return "";
        return string.Join(";", pairs.Select(p => $"{p.Key}={p.Value}")) + ";";
    }

    #endregion

    #region Path Resolution & Batching

    private (RegistryKey rootKey, string subKeyPath) ParseKeyPath(string keyPath)
    {
        var parts = keyPath.Split('\\', 2);
        if (parts.Length < 2)
            throw new ArgumentException($"Invalid registry key path: {keyPath}");

        var hive = parts[0].ToUpperInvariant();

        if ((hive == "HKEY_CURRENT_USER" || hive == "HKCU")
            && interactiveUserService.IsOtsElevation
            && interactiveUserService.InteractiveUserSid != null)
        {
            var redirectedSubKey = $"{interactiveUserService.InteractiveUserSid}\\{parts[1]}";
            return (Registry.Users, redirectedSubKey);
        }

        var rootKey = hive switch
        {
            "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
            "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
            "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
            "HKEY_USERS" or "HKU" => Registry.Users,
            "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
            _ => throw new ArgumentException($"Invalid registry hive: {parts[0]}"),
        };

        return (rootKey, parts[1]);
    }

    private RegistryKey GetHiveFromPath(string keyPath)
    {
        var parts = keyPath.Split('\\', 2);
        var hive = parts[0].ToUpperInvariant();

        if ((hive == "HKEY_CURRENT_USER" || hive == "HKCU")
            && interactiveUserService.IsOtsElevation
            && interactiveUserService.InteractiveUserSid != null)
        {
            return Registry.Users;
        }

        return hive switch
        {
            "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
            "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
            "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
            "HKEY_USERS" or "HKU" => Registry.Users,
            "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
            _ => throw new ArgumentException($"Unrecognized registry hive: '{hive}' in path '{keyPath}'"),
        };
    }

    public Dictionary<string, object?> GetBatchValues(IEnumerable<(string keyPath, string? valueName)> queries)
    {
        var results = new Dictionary<string, object?>();
        var queriesByHive = queries.GroupBy(q => GetHiveFromPath(q.keyPath));

        foreach (var hiveGroup in queriesByHive)
        {
            var rootKey = hiveGroup.Key;

            foreach (var (keyPath, valueName) in hiveGroup)
            {
                try
                {
                    var (_, subKeyPath) = ParseKeyPath(keyPath);
                    using var subKey = rootKey.OpenSubKey(subKeyPath, false);

                    var resultKey = valueName == null
                        ? $"{keyPath}\\__KEY_EXISTS__"
                        : $"{keyPath}\\{valueName}";

                    if (valueName == null)
                    {
                        results[resultKey] = subKey != null;
                    }
                    else
                    {
                        results[resultKey] = subKey?.GetValue(valueName);
                    }
                }
                catch (Exception ex)
                {
                    logService.LogDebug($"[WindowsRegistryService] Failed to get batch value for '{keyPath}\\{valueName}': {ex.Message}");
                    var resultKey = valueName == null
                        ? $"{keyPath}\\__KEY_EXISTS__"
                        : $"{keyPath}\\{valueName}";
                    results[resultKey] = null;
                }
            }
        }

        return results;
    }

    #endregion

    #region Value Comparison

    private static bool CompareValues(object? current, object? desired)
    {
        return current switch
        {
            null => desired == null,
            bool b when desired is int d => (b ? 1 : 0) == d,
            byte b when desired is int d => b == d,
            byte b when desired is byte d => b == d,
            int i when desired is int d => i == d,
            int i when desired is long d => i == d,
            int i when desired is byte d => i == d,
            long l when desired is long d => l == d,
            long l when desired is int d => l == d,
            string s when desired is string ds => s.Equals(
                ds,
                StringComparison.OrdinalIgnoreCase
            ),
            byte[] ba when desired is byte[] dba => ba.SequenceEqual(dba),
            _ => current.Equals(desired),
        };
    }

    #endregion
}