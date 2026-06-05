// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.Win32;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Utilities.Services;

public class StartMenuService(
    IScheduledTaskService scheduledTaskService,
    ILogService logService,
    IInteractiveUserService interactiveUserService,
    IProcessExecutor processExecutor,
    IFileSystemService fileSystemService,
    IWindowsRegistryService windowsRegistryService) : IActionCommandProvider
{
    #region Public API

    public async Task CleanWindows10StartMenuAsync()
    {
        try
        {
            logService.Log(LogLevel.Info, "Starting Windows 10 Start Menu cleaning process");

            await Task.Run(async () =>
                await CleanWindows10StartMenu(scheduledTaskService, logService).ConfigureAwait(false)
            ).ConfigureAwait(false);

            logService.Log(LogLevel.Info, "Windows 10 Start Menu cleaned successfully");
        }
        catch (Exception ex)
        {
            logService.Log(
                LogLevel.Error,
                $"Error cleaning Windows 10 Start Menu: {ex.Message}"
            );
            throw;
        }
    }

    public async Task CleanWindows11StartMenuAsync()
    {
        try
        {
            logService.Log(LogLevel.Info, "Starting Windows 11 Start Menu cleaning process");

            await CleanWindows11StartMenuCoreAsync(logService).ConfigureAwait(false);

            logService.Log(LogLevel.Info, "Windows 11 Start Menu cleaned successfully");
        }
        catch (Exception ex)
        {
            logService.Log(
                LogLevel.Error,
                $"Error cleaning Windows 11 Start Menu: {ex.Message}"
            );
            throw;
        }
    }

    #endregion

    #region Windows 11 Operations

    private async Task CleanWindows11StartMenuCoreAsync(ILogService? logService = null)
    {
        try
        {
            await SetConfigureStartPinsAsync(@"HKLM\SOFTWARE\Microsoft\PolicyManager\current\device\Start").ConfigureAwait(false);
            await SetConfigureStartPinsAsync(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\Explorer").ConfigureAwait(false);

            string localAppData = interactiveUserService.GetInteractiveUserFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            );
            string startMenuLocalStatePath = fileSystemService.CombinePath(
                localAppData,
                "Packages",
                "Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy",
                "LocalState"
            );

            if (fileSystemService.DirectoryExists(startMenuLocalStatePath))
            {
                string startBinPath = fileSystemService.CombinePath(startMenuLocalStatePath, "start.bin");
                if (fileSystemService.FileExists(startBinPath))
                {
                    fileSystemService.DeleteFile(startBinPath);
                }

                string start2BinPath = fileSystemService.CombinePath(startMenuLocalStatePath, "start2.bin");
                if (fileSystemService.FileExists(start2BinPath))
                {
                    fileSystemService.DeleteFile(start2BinPath);
                }
            }

            CleanOtherUsersStartMenuFiles(logService);

            TerminateStartMenuExperienceHost();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error cleaning Windows 11 Start Menu: {ex.Message}", ex);
        }
    }

    private async Task SetConfigureStartPinsAsync(string keyPath)
    {
        var regArgs = $"add \"{keyPath}\" /v \"ConfigureStartPins\" /t REG_SZ /d \"{{\\\"pinnedList\\\":[]}}\" /f";
        var result = await processExecutor.ExecuteAsync("reg.exe", regArgs).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new Exception(
                $"Failed to add ConfigureStartPins to '{keyPath}'. Exit code: {result.ExitCode}. Error: {result.StandardError}"
            );
        }
    }

    #endregion

    #region Windows 10 Operations

    private async Task CleanWindows10StartMenu(
        IScheduledTaskService? scheduledTaskService = null,
        ILogService? logService = null
    )
    {
        try
        {
            if (fileSystemService.FileExists(StartMenuLayouts.Win10StartLayoutPath))
            {
                fileSystemService.DeleteFile(StartMenuLayouts.Win10StartLayoutPath);
            }

            fileSystemService.CreateDirectory(
                fileSystemService.GetDirectoryName(StartMenuLayouts.Win10StartLayoutPath)!
            );

            fileSystemService.WriteAllText(
                StartMenuLayouts.Win10StartLayoutPath,
                StartMenuLayouts.Windows10Layout
            );

            if (scheduledTaskService != null)
            {
                logService?.LogInformation(
                    "Setting up scheduled tasks for all existing users..."
                );
                await SetupScheduledTasksForAllUsersWindows10(scheduledTaskService, logService).ConfigureAwait(false);
            }

            await ApplyWindows10LayoutToCurrentUser().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error cleaning Windows 10 Start Menu: {ex.Message}", ex);
        }
    }

    private async Task ApplyWindows10LayoutToCurrentUser()
    {
        var keyPath = @"HKCU\SOFTWARE\Policies\Microsoft\Windows\Explorer";
        windowsRegistryService.SetValue(keyPath, "LockedStartLayout", 1, RegistryValueKind.DWord);
        windowsRegistryService.SetValue(keyPath, "StartLayoutFile", StartMenuLayouts.Win10StartLayoutPath, RegistryValueKind.String);

        TerminateStartMenuExperienceHost();

        await Task.Delay(3000).ConfigureAwait(false);

        windowsRegistryService.SetValue(keyPath, "LockedStartLayout", 0, RegistryValueKind.DWord);

        TerminateStartMenuExperienceHost();
    }

    private async Task SetupScheduledTasksForAllUsersWindows10(
        IScheduledTaskService scheduledTaskService,
        ILogService? logService = null
    )
    {
        try
        {
            var currentUsername = interactiveUserService.InteractiveUserName;
            var otherUsernames = GetOtherUsernames();

            logService?.LogInformation(
                $"Creating scheduled tasks for {otherUsernames.Count} other users (excluding current user: {currentUsername})"
            );

            if (otherUsernames.Count == 0)
            {
                logService?.LogInformation(
                    "No other users found to create scheduled tasks for"
                );
                return;
            }

            var tasks = new List<Task>();

            foreach (var username in otherUsernames)
            {
                try
                {
                    var taskName = $"CleanStartMenu_{username}";

                    var command =
                        $"-ExecutionPolicy Bypass -WindowStyle Hidden -Command \"$loggedInUser = (Get-WmiObject -Class Win32_ComputerSystem).UserName.Split('\\')[1]; $userSID = (New-Object System.Security.Principal.NTAccount($loggedInUser)).Translate([System.Security.Principal.SecurityIdentifier]).Value; reg add ('HKU\\' + $userSID + '\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer') /v LockedStartLayout /t REG_DWORD /d 1 /f; reg add ('HKU\\' + $userSID + '\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer') /v StartLayoutFile /t REG_SZ /d 'C:\\Users\\Default\\AppData\\Local\\Microsoft\\Windows\\Shell\\LayoutModification.xml' /f; Stop-Process -Name 'StartMenuExperienceHost' -Force -ErrorAction SilentlyContinue; Start-Sleep 10; Set-ItemProperty -Path ('Registry::HKU\\' + $userSID + '\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer') -Name 'LockedStartLayout' -Value 0; Stop-Process -Name 'StartMenuExperienceHost' -Force -ErrorAction SilentlyContinue; schtasks /delete /tn 'EvolveOS\\{taskName}' /f\"";

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await scheduledTaskService.CreateUserLogonTaskAsync(
                                taskName,
                                command,
                                username,
                                false
                            ).ConfigureAwait(false);
                            logService?.LogInformation(
                                $"Successfully created scheduled task '{taskName}' for user '{username}'"
                            );
                        }
                        catch (Exception ex)
                        {
                            logService?.LogError(
                                $"Failed to create scheduled task for user '{username}': {ex.Message}"
                            );
                        }
                    }));
                }
                catch (Exception ex)
                {
                    logService?.LogError(
                        $"Error setting up scheduled task for user '{username}': {ex.Message}"
                    );
                }
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logService?.LogError(
                $"Error in SetupScheduledTasksForAllUsersWindows10: {ex.Message}"
            );
        }
    }

    #endregion

    #region Start Menu Utilities & User Profiles

    public void TerminateStartMenuExperienceHost()
    {
        var startMenuProcesses = Process.GetProcessesByName("StartMenuExperienceHost");
        foreach (var process in startMenuProcesses)
        {
            try
            {
                process.Kill();
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Debug, $"Could not terminate StartMenuExperienceHost process: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private void CleanOtherUsersStartMenuFiles(ILogService? logService = null)
    {
        try
        {
            var currentUsername = interactiveUserService.InteractiveUserName;
            var otherUsernames = GetOtherUsernames();

            logService?.Log(
                LogLevel.Info,
                $"Cleaning Start Menu files for {otherUsernames.Count} other users (excluding current user: {currentUsername})"
            );

            if (otherUsernames.Count == 0)
            {
                logService?.Log(
                    LogLevel.Info,
                    "No other users found to clean Start Menu files for"
                );
                return;
            }

            foreach (var username in otherUsernames)
            {
                try
                {
                    string userProfilePath = $"C:\\Users\\{username}";
                    string start2BinPath = fileSystemService.CombinePath(
                        userProfilePath,
                        "AppData",
                        "Local",
                        "Packages",
                        "Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy",
                        "LocalState",
                        "start2.bin"
                    );

                    logService?.Log(
                        LogLevel.Info,
                        $"Attempting to delete start2.bin for user: {username}"
                    );

                    if (fileSystemService.FileExists(start2BinPath))
                    {
                        fileSystemService.DeleteFile(start2BinPath);
                        logService?.Log(
                            LogLevel.Info,
                            $"Successfully deleted start2.bin for user: {username}"
                        );
                    }
                    else
                    {
                        logService?.Log(
                            LogLevel.Info,
                            $"start2.bin file not found for user: {username} (may not exist or user hasn't used Start Menu yet)"
                        );
                    }

                    string startBinPath = fileSystemService.CombinePath(
                        userProfilePath,
                        "AppData",
                        "Local",
                        "Packages",
                        "Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy",
                        "LocalState",
                        "start.bin"
                    );

                    if (fileSystemService.FileExists(startBinPath))
                    {
                        fileSystemService.DeleteFile(startBinPath);
                        logService?.Log(
                            LogLevel.Info,
                            $"Successfully deleted start.bin for user: {username}"
                        );
                    }
                }
                catch (Exception ex)
                {
                    logService?.Log(
                        LogLevel.Warning,
                        $"Failed to delete Start Menu files for user {username}: {ex.Message}"
                    );
                }
            }

            logService?.Log(
                LogLevel.Info,
                "Completed cleaning Start Menu files for other users"
            );
        }
        catch (Exception ex)
        {
            logService?.Log(
                LogLevel.Error,
                $"Error during other users Start Menu cleaning: {ex.Message}"
            );
        }
    }

    private List<string> GetOtherUsernames()
    {
        var usernames = new List<string>();
        string currentUsername = interactiveUserService.InteractiveUserName;

        try
        {
            var profileListPath = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";
            var subKeyNames = windowsRegistryService.GetSubKeyNames(profileListPath);

            foreach (string sidKey in subKeyNames)
            {
                if (sidKey.StartsWith("S-1-5-21-")) // User SID pattern
                {
                    var profilePath = windowsRegistryService.GetValue(
                        $@"{profileListPath}\{sidKey}", "ProfileImagePath")?.ToString();

                    if (!string.IsNullOrEmpty(profilePath))
                    {
                        string username = fileSystemService.GetFileName(profilePath);
                        // Skip current user and system accounts
                        if (username != currentUsername && !IsSystemAccount(username))
                        {
                            usernames.Add(username);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Failed to enumerate other user profiles: {ex.Message}");
        }

        return usernames;
    }

    private bool IsSystemAccount(string username)
    {
        string[] systemAccounts = { "Public", "Default", "All Users", "Default User" };
        return systemAccounts.Contains(username, StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    #region IActionCommandProvider Implementation

    private static readonly HashSet<string> _supportedCommands = new(StringComparer.Ordinal)
    {
        nameof(CleanWindows10StartMenuAsync),
        nameof(CleanWindows11StartMenuAsync)
    };

    public IReadOnlySet<string> SupportedCommands => _supportedCommands;

    public Task ExecuteCommandAsync(string commandName) => commandName switch
    {
        nameof(CleanWindows10StartMenuAsync) => CleanWindows10StartMenuAsync(),
        nameof(CleanWindows11StartMenuAsync) => CleanWindows11StartMenuAsync(),
        _ => throw new NotSupportedException($"Command '{commandName}' is not supported by {nameof(StartMenuService)}")
    };

    #endregion
}