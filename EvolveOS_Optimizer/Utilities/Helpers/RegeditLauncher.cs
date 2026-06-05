// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.Win32;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Utilities.Extensions;

namespace EvolveOS_Optimizer.Utilities.Helpers;

public class RegeditLauncher(
    IInteractiveUserService interactiveUserService,
    IProcessExecutor processExecutor,
    ILogService logService) : IRegeditLauncher
{

    public bool KeyExists(string registryPath)
    {
        try
        {
            var (root, subKey) = ParsePath(registryPath);
            if (root == null || subKey == null) return false;
            using var key = root.OpenSubKey(subKey);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    private (RegistryKey? root, string? subKey) ParsePath(string path)
    {
        var separatorIndex = path.IndexOf('\\');
        if (separatorIndex < 0) return (null, null);

        var hive = path[..separatorIndex].ToUpperInvariant();
        var subKey = path[(separatorIndex + 1)..];

        if ((hive == "HKEY_CURRENT_USER" || hive == "HKCU")
            && interactiveUserService.IsOtsElevation
            && interactiveUserService.InteractiveUserSid != null)
        {
            return (Registry.Users, $@"{interactiveUserService.InteractiveUserSid}\{subKey}");
        }

        RegistryKey? root = hive switch
        {
            "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
            "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
            "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
            "HKEY_USERS" or "HKU" => Registry.Users,
            "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
            _ => null
        };

        return (root, subKey);
    }

    public void OpenAtPath(string registryPath)
    {
        try
        {
            var navigatePath = registryPath;

            if (navigatePath.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase))
                navigatePath = $"HKEY_CURRENT_USER\\{navigatePath[5..]}";
            else if (navigatePath.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
                navigatePath = $"HKEY_LOCAL_MACHINE\\{navigatePath[5..]}";

            var fullPath = navigatePath.StartsWith("Computer\\", StringComparison.OrdinalIgnoreCase)
                ? navigatePath
                : $"Computer\\{navigatePath}";

            bool isOts = interactiveUserService.IsOtsElevation
                && interactiveUserService.InteractiveUserSid != null
                && interactiveUserService.HasInteractiveUserToken;

            if (isOts)
            {
                var sid = interactiveUserService.InteractiveUserSid!;
                using var key = Registry.Users.CreateSubKey(
                    $@"{sid}\Software\Microsoft\Windows\CurrentVersion\Applets\Regedit");
                key?.SetValue("LastKey", fullPath);

                interactiveUserService.LaunchProcessAsInteractiveUser("regedit.exe");
            }
            else
            {
                using var key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit");
                key?.SetValue("LastKey", fullPath);

                processExecutor.ShellExecuteAsync("regedit.exe").FireAndForget(logService);
            }
        }
        catch
        {
            // Silently ignore
        }
    }
}
