using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Storage;
using EvolveOS_Optimizer.Utilities.Tweaks.DefenderManager;
using EvolveOS_Optimizer.Views;
using Microsoft.Win32;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace EvolveOS_Optimizer.Utilities.Tweaks
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct UIAction
    {
        internal const uint SPI_SETMOUSESPEED = 0x0071;
        internal const uint SPI_SETKEYBOARDDELAY = 0x0017;
        internal const uint SPI_SETKEYBOARDSPEED = 0x000B;
        internal const uint SPI_SETMOUSE = 0x0004;
    };

    internal sealed class SystemTweaks : FirewallManager
    {
        private static bool _isNetshState = false, _isBluetoothStatus = false, _isTickState = false;
        private static string _currentPowerGuid = string.Empty;

        internal static Dictionary<string, object> ControlStates = new Dictionary<string, object>();
        private readonly ControlWriterManager _сontrolWriter = new ControlWriterManager(ControlStates);

        public SystemTweaks() => _currentPowerGuid = RegistryHelp.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActivePowerScheme", string.Empty);

        internal void AnalyzeAndUpdate()
        {
            _сontrolWriter.Slider[1] = RegistryHelp.GetValue<double>(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSensitivity", 10);

            _сontrolWriter.Slider[2] = RegistryHelp.GetValue<double>(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "KeyboardDelay", 1);

            _сontrolWriter.Slider[3] = RegistryHelp.GetValue<double>(@"HKEY_CURRENT_USER\Control Panel\Keyboard", "KeyboardSpeed", 31);

            _сontrolWriter.Button[1] =
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed", "0") ||
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold1", "0") ||
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold2", "0");

            _сontrolWriter.Button[2] =
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Control Panel\Accessibility\StickyKeys", "Flags", "26") ||
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Control Panel\Accessibility\Keyboard Response", "Flags", "26");

            _сontrolWriter.Button[3] = File.Exists(Path.Combine(PathLocator.Folders.SystemDrive, @"Windows\System32\smartscreen.exe"));

            _сontrolWriter.Button[4] =
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "PromptOnSecureDesktop", "0") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", "0") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableInstallerDetection", "0") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableSecureUIAPaths", "0") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "FilterAdministratorToken", "0") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableVirtualization", "0") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorAdmin", "0");

            _сontrolWriter.Button[5] =
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.SecurityAndMaintenance", "Enabled", "0");

            _сontrolWriter.Button[6] =
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsStore", "AutoDownload", "2");

            try
            {
                using RegistryKey? regKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e96c-e325-11ce-bfc1-08002be10318}");
                if (regKey != null)
                {
                    foreach (var subKeyName in regKey.GetSubKeyNames())
                    {
                        if (subKeyName == "Properties")
                        {
                            continue;
                        }

                        using RegistryKey? subKey = regKey.OpenSubKey(subKeyName);
                        if (subKey != null)
                        {
                            if (subKey.GetValue("DriverDesc") is string driverDesc && driverDesc.Equals("Realtek High Definition Audio", StringComparison.OrdinalIgnoreCase))
                            {
                                using RegistryKey? powerSettingsKey = subKey.OpenSubKey("PowerSettings");
                                if (powerSettingsKey != null)
                                {
                                    if (!(powerSettingsKey.GetValue("ConservationIdleTime") is byte[] conservationIdleTime) || !(powerSettingsKey.GetValue("IdlePowerState") is byte[] idlePowerState) || !(powerSettingsKey.GetValue("PerformanceIdleTime") is byte[] performanceIdleTime))
                                    {
                                        _сontrolWriter.Button[7] = false;
                                    }
                                    else
                                    {
                                        _сontrolWriter.Button[7] = conservationIdleTime?[0].ToString() != "255" || idlePowerState?[0].ToString() != "0" || performanceIdleTime?[0].ToString() != "255";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); }

            _сontrolWriter.Button[8] =
              RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\7516b95f-f776-4464-8c53-06167f40cc99\8EC4B3A5-6868-48c2-BE75-4F3044BE88A7", "Attributes", "2");

            _сontrolWriter.Button[9] =
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", "0") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", "0");

            _сontrolWriter.Button[10] =
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "AutoEndTasks", "1");

            _сontrolWriter.Button[11] =
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Internet Explorer\Security", "DisableSecuritySettingsCheck", "1") ||
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Internet Settings\Zones\3", "1806", "0");

            _сontrolWriter.Button[12] = IsTaskEnabled(TaskStorage.memoryDiagTasks);

            _сontrolWriter.Button[13] = _isNetshState;

            _сontrolWriter.Button[14] =
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "LargeSystemCache", "1");

            _сontrolWriter.Button[15] =
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "Startupdelayinmsec", "0");

            _сontrolWriter.Button[16] =
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowFrequent", "0") ||
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowRecent", "0") ||
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackDocs", "0") ||
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", "0");

            _сontrolWriter.Button[17] =
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay", "1");

            _сontrolWriter.Button[18] =
                !RegistryHelp.GetValue($@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{_currentPowerGuid}", "Description", string.Empty).Contains("-18") &&
                !RegistryHelp.GetValue($@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{_currentPowerGuid}", "FriendlyName", string.Empty).Contains("-19");

            _сontrolWriter.Button[19] = _isBluetoothStatus;

            _сontrolWriter.Button[20] =
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\mpssvc", "Start", "4");

            _сontrolWriter.Button[21] =
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AutoGameModeEnabled", "0") ||
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode", "0");

            _сontrolWriter.Button[22] =
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", "0") ||
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", "0") ||
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", "0");

            _сontrolWriter.Button[23] =
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", "1") ||
                RegistryHelp.CheckValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", "0") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground", "2");

            _сontrolWriter.Button[24] =
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager", "MiscPolicyInfo", "2") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager", "PassedPolicy", "0") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager", "ShippedWithReserves", "0");

            _сontrolWriter.Button[25] = _isTickState;

            _сontrolWriter.Button[26] =
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PCHC", "PreviousUninstall", "1", true) ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\PCHealthCheck", "installed", "1", true);

            _сontrolWriter.Button[27] = IsTaskEnabled(TaskStorage.winInsiderTasks);

            _сontrolWriter.Button[28] = !IsTaskEnabled(TaskStorage.defragTask) ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Dfrg\BootOptimizeFunction", "Enable", "N") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\services\defragsvc", "Start", "2");

            _сontrolWriter.Button[29] =
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", "0");

            _сontrolWriter.Button[30] =
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Session Manager\Memory Management", "ClearPageFileAtShutdown", "0");

            _сontrolWriter.Button[31] =
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*PMARPOffload", "1") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*FlowControl", "3") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*InterruptModeration", "1") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*IPChecksumOffloadIPv4", "3") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*LsoV2IPv4", "1") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*LsoV2IPv6", "1") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*PMNSOffload", "1") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*TCPChecksumOffloadIPv4", "3") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*TCPChecksumOffloadIPv6", "3") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*UDPChecksumOffloadIPv4", "3") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*UDPChecksumOffloadIPv6", "3") ||
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*WakeOnMagicPacket", "1");

            _сontrolWriter.Button[32] =
                RegistryHelp.CheckValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\AFD\Parameters", "FastSendDatagramThreshold", "65536");
        }


        internal static void ViewBluetoothStatus()
        {
            try
            {
                using ManagementObjectCollection managementObjCollection = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_PnPEntity WHERE Service='BthLEEnum'").Get();
                _isBluetoothStatus = managementObjCollection.Cast<ManagementObject>().Any();
            }
            catch { _isBluetoothStatus = false; }
        }

        internal static void ViewNetshState()
        {
            string getStateNetsh = CommandExecutor.GetCommandOutput("/c chcp 65001 & netsh int teredo show state & netsh int ipv6 isatap show state & netsh int isatap show state & netsh int ipv6 6to4 show state", false).Result;
            _isNetshState = getStateNetsh.Contains("default") || getStateNetsh.Contains("enabled");
        }

        internal static void ViewConfigTick()
        {
            string output = CommandExecutor.GetCommandOutput(PathLocator.Executable.BcdEdit).Result;
            _isTickState = !Regex.IsMatch(output, @"(?is)(?=.*\bdisabledynamictick\s+(yes|true))(?=.*\buseplatformclock\s+(no|false))", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);
        }

        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint _uiAction, uint _uiParam, uint _pvParam, uint _fWinIni);
        internal void ApplyTweaksSlider(string tweak, uint value)
        {
            INIManager.TempWrite(INIManager.TempTweaksSys, tweak, value);

            switch (tweak)
            {
                case "Slider1":
                    SystemParametersInfo(UIAction.SPI_SETMOUSESPEED, value, value, 2);
                    RegistryHelp.Write(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSensitivity", value, RegistryValueKind.String);
                    break;
                case "Slider2":
                    SystemParametersInfo(UIAction.SPI_SETKEYBOARDDELAY, value, value, 2);
                    RegistryHelp.Write(Registry.CurrentUser, @"Control Panel\Keyboard", "KeyboardDelay", value, RegistryValueKind.String);
                    break;
                case "Slider3":
                    SystemParametersInfo(UIAction.SPI_SETKEYBOARDSPEED, value, value, 2);
                    RegistryHelp.Write(Registry.CurrentUser, @"Control Panel\Keyboard", "KeyboardSpeed", value, RegistryValueKind.String);
                    break;
            }
        }


        [DllImport("user32.dll")]
        private static extern bool SystemParametersInfo(uint _uiAction, uint _uiParam, uint[] _pvParam, uint _fWinIni);
        internal async void ApplyTweaks(string tweak, bool isDisabled, bool canShowWindow = true)
        {
            INIManager.TempWrite(INIManager.TempTweaksSys, tweak, isDisabled);

            switch (tweak)
            {
                case "TglButton1":
                    SystemParametersInfo(UIAction.SPI_SETMOUSE, 0, isDisabled ? new uint[3] : new uint[] { 1, 6, 10 }, 2);
                    RegistryHelp.Write(Registry.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", isDisabled ? "0" : "1", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", isDisabled ? "0" : "6", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", isDisabled ? "0" : "10", RegistryValueKind.String);
                    break;
                case "TglButton2":
                    RegistryHelp.Write(Registry.CurrentUser, @"Control Panel\Accessibility\StickyKeys", "Flags", isDisabled ? "26" : "507", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.CurrentUser, @"Control Panel\Accessibility\Keyboard Response", "Flags", isDisabled ? "26" : "58", RegistryValueKind.String);
                    break;
                case "TglButton3":
                    await BlockWDefender(isDisabled);

                    byte[] resourceData = ArchiveManager.GetResourceBytes("NSudoLC.gz");
                    if (resourceData.Length > 0) ArchiveManager.Unarchive(PathLocator.Executable.NSudo, resourceData);

                    resourceData = ArchiveManager.GetResourceBytes("DisablingWD.gz");
                    if (resourceData.Length > 0) ArchiveManager.Unarchive(PathLocator.Executable.DisablingWD, resourceData);

                    if (canShowWindow)
                    {
                        var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

                        if (dispatcherQueue != null)
                        {
                            dispatcherQueue.TryEnqueue(async () =>
                            {
                                try
                                {
                                    OverlayWindow overlayWindow = new OverlayWindow();
                                    overlayWindow.Activate();

                                    NotificationManager.Show(isDisabled ? "warn" : "info", isDisabled ? "warn_wd_noty" : "info_wd_noty").Perform();

                                    await Task.Run(() => WindowsDefender.SetProtectionStateAsync(isDisabled));

                                    if (!isDisabled)
                                    {
                                        NotificationManager.Show().WithDuration(300).Restart();

                                        _ = Task.Run(async () => {
                                            await Task.Delay(10000);
                                            await CommandExecutor.RunCommand($"/c del /f \"{PathLocator.Executable.NSudo}\"");
                                        });
                                    }

                                    overlayWindow.Close();
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Defender Tweak Error: {ex.Message}");
                                }
                            });
                        }
                        else
                        {
                            await Task.Run(() => WindowsDefender.SetProtectionStateAsync(isDisabled));
                        }
                    }
                    else
                    {
                        await Task.Run(() => WindowsDefender.SetProtectionStateAsync(isDisabled));
                    }
                    break;
                case "TglButton4":
                    RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "ConsentPromptBehaviorAdmin", isDisabled ? 0 : 5, RegistryValueKind.DWord);
                    RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableInstallerDetection", isDisabled ? 0 : 1, RegistryValueKind.DWord);
                    RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", isDisabled ? 0 : 1, RegistryValueKind.DWord);
                    RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableSecureUIAPaths", isDisabled ? 0 : 1, RegistryValueKind.DWord);
                    RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableVirtualization", isDisabled ? 0 : 1, RegistryValueKind.DWord);
                    RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "FilterAdministratorToken", isDisabled ? 0 : 1, RegistryValueKind.DWord);
                    RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "PromptOnSecureDesktop", isDisabled ? 0 : 1, RegistryValueKind.DWord);
                    break;
                case "TglButton5":
                    RegistryHelp.Write(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.SecurityAndMaintenance", "Enabled", isDisabled ? 0 : 1, RegistryValueKind.DWord);
                    break;
                case "TglButton6":
                    if (isDisabled)
                    {
                        RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\WindowsStore", "AutoDownload", 2, RegistryValueKind.DWord);
                    }
                    else
                    {
                        RegistryHelp.DeleteValue(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\WindowsStore", "AutoDownload");
                    }
                    break;
                case "TglButton7":
                    try
                    {
                        using RegistryKey? regKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e96c-e325-11ce-bfc1-08002be10318}");
                        if (regKey != null)
                        {
                            foreach (var subKeyName in regKey.GetSubKeyNames())
                            {
                                using RegistryKey? subKey = regKey.OpenSubKey(subKeyName);
                                if (subKey != null)
                                {
                                    if (subKey.GetValue("DriverDesc") is string driverDesc && driverDesc.Equals("Realtek High Definition Audio", StringComparison.OrdinalIgnoreCase))
                                    {
                                        RegistryHelp.Write(Registry.LocalMachine, $@"{@"SYSTEM\CurrentControlSet\Control\Class\{4d36e96c-e325-11ce-bfc1-08002be10318}"}\{subKeyName}\PowerSettings", "ConservationIdleTime", isDisabled ? new byte[] { 0xFF, 0xFF, 0xFF, 0xFF } : new byte[] { 0x0a, 0x00, 0x00, 0x00 }, RegistryValueKind.Binary);
                                        RegistryHelp.Write(Registry.LocalMachine, $@"{@"SYSTEM\CurrentControlSet\Control\Class\{4d36e96c-e325-11ce-bfc1-08002be10318}"}\{subKeyName}\PowerSettings", "IdlePowerState", isDisabled ? new byte[] { 0x00, 0x00, 0x00, 0x00 } : new byte[] { 0x03, 0x00, 0x00, 0x00 }, RegistryValueKind.Binary);
                                        RegistryHelp.Write(Registry.LocalMachine, $@"{@"SYSTEM\CurrentControlSet\Control\Class\{4d36e96c-e325-11ce-bfc1-08002be10318}"}\{subKeyName}\PowerSettings", "PerformanceIdleTime", isDisabled ? new byte[] { 0xFF, 0xFF, 0xFF, 0xFF } : new byte[] { 0x0a, 0x00, 0x00, 0x00 }, RegistryValueKind.Binary);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) { ErrorLogging.LogDebug(ex); }
                    break;
                case "TglButton8":
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\7516b95f-f776-4464-8c53-06167f40cc99\8EC4B3A5-6868-48c2-BE75-4F3044BE88A7", "Attributes", isDisabled ? 2 : 1, RegistryValueKind.DWord);
                    break;
                case "TglButton9":
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", isDisabled ? 0 : 1, RegistryValueKind.DWord);
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", isDisabled ? 0 : 1, RegistryValueKind.DWord);
                    await CommandExecutor.RunCommand(@$"/c powercfg.exe -h {(isDisabled ? "off" : "on")}");
                    break;
                case "TglButton10":
                    if (isDisabled)
                    {
                        RegistryHelp.Write(Registry.CurrentUser, @"Control Panel\Desktop", "AutoEndTasks", "1", RegistryValueKind.String);
                    }
                    else
                    {
                        RegistryHelp.DeleteValue(Registry.CurrentUser, @"Control Panel\Desktop", "AutoEndTasks");
                    }
                    break;
                case "TglButton11":
                    if (isDisabled)
                    {
                        RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\Internet Explorer\Security", "DisableSecuritySettingsCheck", 1, RegistryValueKind.DWord);
                        RegistryHelp.Write(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Zones\3", "1806", 0, RegistryValueKind.DWord);
                    }
                    else
                    {
                        RegistryHelp.DeleteValue(Registry.LocalMachine, @"SOFTWARE\Microsoft\Internet Explorer\Security", "DisableSecuritySettingsCheck");
                        RegistryHelp.DeleteValue(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Internet Settings\Zones\3", "1806");
                    }
                    break;
                case "TglButton12":
                    await SetTaskState(!isDisabled, TaskStorage.memoryDiagTasks);
                    break;
                case "TglButton13":
                    string argStateNetshSecond, argStateNetsh;
                    if (isDisabled)
                    {
                        _isNetshState = false;
                        argStateNetsh = argStateNetshSecond = @"disabled";
                    }
                    else
                    {
                        _isNetshState = true;
                        argStateNetshSecond = @"enabled";
                        argStateNetsh = @"default";
                    }
                    await CommandExecutor.RunCommand($"/c netsh int teredo set state {argStateNetsh} & netsh int ipv6 6to4 set state state = {argStateNetsh} undoonstop = {argStateNetsh} & netsh int ipv6 isatap set state state = {argStateNetsh} & netsh int ipv6 set privacy state = {argStateNetshSecond} & netsh int ipv6 set global randomizeidentifier = {argStateNetshSecond} & netsh int isatap set state {argStateNetsh}");
                    break;
                case "TglButton14":
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "LargeSystemCache", isDisabled ? 1 : 0, RegistryValueKind.DWord);
                    break;
                case "TglButton15":
                    if (isDisabled)
                    {
                        RegistryHelp.Write(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "Startupdelayinmsec", 0, RegistryValueKind.DWord);
                    }
                    else
                    {
                        RegistryHelp.DeleteValue(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "Startupdelayinmsec");
                    }
                    break;
                case "TglButton16":
                    if (isDisabled)
                    {
                        RegistryHelp.Write(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowFrequent", 0, RegistryValueKind.DWord);
                        RegistryHelp.Write(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowRecent", 0, RegistryValueKind.DWord);
                        RegistryHelp.Write(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackDocs", 0, RegistryValueKind.DWord);
                        RegistryHelp.Write(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0, RegistryValueKind.DWord);
                    }
                    else
                    {
                        RegistryHelp.DeleteValue(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowFrequent");
                        RegistryHelp.DeleteValue(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowRecent");
                        RegistryHelp.DeleteValue(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackDocs");
                        RegistryHelp.DeleteValue(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs");
                    }
                    break;
                case "TglButton17":
                    RegistryHelp.Write(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay", isDisabled ? 1 : 0, RegistryValueKind.DWord);
                    break;
                case "TglButton18":
                    SetPowercfg(isDisabled);
                    break;
                case "TglButton19":
                    await CommandExecutor.RunCommand(@"Add-Type -AssemblyName System.Runtime.WindowsRuntime
                        $asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | ? { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
                        Function Await($WinRtTask, $ResultType) {
                            $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
                            $netTask = $asTask.Invoke($null, @($WinRtTask))
                            $netTask.Wait(-1) | Out-Null
                            $netTask.Result
                        }

                        [Windows.Devices.Radios.Radio,Windows.System.Devices,ContentType=WindowsRuntime] | Out-Null
                        [Windows.Devices.Radios.RadioAccessStatus,Windows.System.Devices,ContentType=WindowsRuntime] | Out-Null
                        Await ([Windows.Devices.Radios.Radio]::RequestAccessAsync()) ([Windows.Devices.Radios.RadioAccessStatus]) | Out-Null
                        $radios = Await ([Windows.Devices.Radios.Radio]::GetRadiosAsync()) ([System.Collections.Generic.IReadOnlyList[Windows.Devices.Radios.Radio]])
                        $bluetooth = $radios | ? { $_.Kind -eq 'Bluetooth' }
                        [Windows.Devices.Radios.RadioState,Windows.System.Devices,ContentType=WindowsRuntime] | Out-Null
                        Await ($bluetooth.SetStateAsync(" + (isDisabled ? "'off'" : "'on'") + ")) ([Windows.Devices.Radios.RadioAccessStatus]) | Out-Null", true);
                    _isBluetoothStatus = !isDisabled;
                    break;
                case "TglButton20":
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\mpssvc", "Start", isDisabled ? 4 : 2, RegistryValueKind.DWord);
                    await CommandExecutor.RunCommand($"/c netsh advfirewall set allprofiles state {(isDisabled ? "off" : "on")}");
                    if (HardwareData.OS.Build.CompareTo(22621.521m) >= 0)
                    {
                        RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\wtd", "Start", isDisabled ? 4 : 2, RegistryValueKind.DWord);
                    }
                    break;
                case "TglButton21":
                    RegistryHelp.Write(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", isDisabled ? 1 : 0, RegistryValueKind.DWord);
                    RegistryHelp.Write(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AllowAutoGameMode", isDisabled ? 1 : 0, RegistryValueKind.DWord);
                    break;
                case "TglButton22":
                    RegistryHelp.Write(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", isDisabled ? 1 : 0, RegistryValueKind.DWord);
                    RegistryHelp.Write(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", isDisabled ? 1 : 0, RegistryValueKind.DWord);
                    RegistryHelp.Write(Registry.CurrentUser, @"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", isDisabled ? 1 : 0, RegistryValueKind.DWord);
                    break;
                case "TglButton23":
                    if (isDisabled)
                    {
                        RegistryHelp.Write(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1, RegistryValueKind.DWord);
                        RegistryHelp.Write(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0, RegistryValueKind.DWord);
                        RegistryHelp.DeleteFolderTree(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy");
                        RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground", 2, RegistryValueKind.DWord);
                    }
                    else
                    {
                        RegistryHelp.DeleteValue(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled");
                        RegistryHelp.DeleteValue(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle");
                        RegistryHelp.DeleteFolderTree(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy");
                    }
                    break;
                case "TglButton24":
                    if (isDisabled)
                    {
                        RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager", "MiscPolicyInfo", 2, RegistryValueKind.DWord);
                        RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager", "PassedPolicy", 0, RegistryValueKind.DWord);
                        RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager", "ShippedWithReserves", 0, RegistryValueKind.DWord);
                    }
                    else
                    {
                        RegistryHelp.DeleteValue(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager", "MiscPolicyInfo");
                        RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager", "PassedPolicy", 1, RegistryValueKind.DWord);
                        RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager", "ShippedWithReserves", 1, RegistryValueKind.DWord);
                    }
                    break;
                case "TglButton25":
                    if (isDisabled)
                    {
                        _isTickState = false;
                        await CommandExecutor.RunCommand($@"{PathLocator.Executable.BcdEdit} /set disabledynamictick yes; {PathLocator.Executable.BcdEdit} /set useplatformclock false", true);
                    }
                    else
                    {
                        _isTickState = true;
                        await CommandExecutor.RunCommand($@"{PathLocator.Executable.BcdEdit} /deletevalue disabledynamictick; {PathLocator.Executable.BcdEdit} /deletevalue useplatformclock", true);
                    }
                    break;
                case "TglButton26":
                    if (isDisabled)
                    {
                        RegistryHelp.DeleteValue(Registry.LocalMachine, @"SOFTWARE\Microsoft\PCHC", "PreviousUninstall");
                        RegistryHelp.DeleteValue(Registry.LocalMachine, @"SOFTWARE\Microsoft\PCHealthCheck", "installed");
                    }
                    else
                    {
                        RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\PCHC", "PreviousUninstall", 1, RegistryValueKind.DWord);
                        RegistryHelp.Write(Registry.LocalMachine, @"SOFTWARE\Microsoft\PCHealthCheck", "installed", 1, RegistryValueKind.DWord);
                    }
                    break;
                case "TglButton27":
                    await SetTaskState(!isDisabled, TaskStorage.winInsiderTasks);
                    break;
                case "TglButton28":
                    await SetTaskState(true, TaskStorage.defragTask);
                    RegistryHelp.Write(Registry.CurrentUser, @"SOFTWARE\Microsoft\Dfrg\BootOptimizeFunction", "Enable", isDisabled ? "N" : "Y", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\services\defragsvc", "Start", 2, RegistryValueKind.DWord);
                    break;
                case "TglButton29":
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", isDisabled ? 1 : 0, RegistryValueKind.DWord);
                    break;
                case "TglButton30":
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\ControlSet001\Control\Session Manager\Memory Management", "ClearPageFileAtShutdown", isDisabled ? 1 : 0, RegistryValueKind.DWord);
                    break;
                case "TglButton31":
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*PMARPOffload", isDisabled ? "0" : "1", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*FlowControl", isDisabled ? "0" : "3", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*InterruptModeration", isDisabled ? "0" : "1", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*IPChecksumOffloadIPv4", isDisabled ? "0" : "3", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*LsoV2IPv4", isDisabled ? "0" : "1", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*LsoV2IPv6", isDisabled ? "0" : "1", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*PMNSOffload", isDisabled ? "0" : "1", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*TCPChecksumOffloadIPv4", isDisabled ? "0" : "3", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*TCPChecksumOffloadIPv6", isDisabled ? "0" : "3", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*UDPChecksumOffloadIPv4", isDisabled ? "0" : "3", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*UDPChecksumOffloadIPv6", isDisabled ? "0" : "3", RegistryValueKind.String);
                    RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\ControlSet001\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0009", "*WakeOnMagicPacket", isDisabled ? "0" : "1", RegistryValueKind.String);
                    NotificationManager.Show("restart").WithDuration(500).Perform();
                    break;
                case "TglButton32":
                    if (isDisabled)
                    {
                        RegistryHelp.Write(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\AFD\Parameters", "FastSendDatagramThreshold", 65536, RegistryValueKind.DWord);
                        NotificationManager.Show("restart").WithDuration(500).Perform();
                    }
                    else
                    {
                        RegistryHelp.DeleteValue(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\AFD\Parameters", "FastSendDatagramThreshold");
                        NotificationManager.Show("restart").WithDuration(500).Perform();
                    }
                    break;
                default:
                    break;
            }
        }

        private static void SetPowercfg(bool isDisabled)
        {
            Task.Run(async () =>
            {
                Process _powercfg = new Process()
                {
                    StartInfo = {
                        FileName = PathLocator.Executable.PowerCfg,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true
                    },
                };

                string? searchScheme = default, unlockFrequency = @"-attributes SUB_PROCESSOR 75b0ae3f-bce0-45a7-8c89-c9611c25e100 -ATTRIB_HIDE";

                try
                {
                    if (isDisabled)
                    {
                        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"root\cimv2\power", "SELECT InstanceID FROM Win32_PowerPlan WHERE IsActive=false"))
                        {
                            foreach (ManagementObject managementObj in searcher.Get().Cast<ManagementObject>())
                            {
                                using (managementObj)
                                {
                                    string rawInstanceId = managementObj["InstanceID"]?.ToString() ?? string.Empty;
                                    var match = Regex.Match(rawInstanceId, @"\{([^)]*)\}");

                                    _currentPowerGuid = match.Success ? match.Groups[1].Value : "00000000-0000-0000-0000-000000000000";

                                    if (RegistryHelp.GetValue($@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{searchScheme}", "Description", string.Empty).Contains("-18") &&
                                        RegistryHelp.GetValue($@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{searchScheme}", "FriendlyName", string.Empty).Contains("-19"))
                                    {
                                        _currentPowerGuid = searchScheme ?? string.Empty;

                                        using (_powercfg)
                                        {
                                            _powercfg.StartInfo.Arguments = $"/setactive {searchScheme}";
                                            _powercfg.Start();

                                            _powercfg.StartInfo.Arguments = unlockFrequency;
                                            _powercfg.Start();
                                        }

                                        break;
                                    }
                                    else
                                    {
                                        searchScheme = string.Empty;
                                    }
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(searchScheme))
                        {
                            byte[] resourceData = ArchiveManager.GetResourceBytes("UltPower.pow");

                            if (resourceData.Length > 0)
                            {
                                ArchiveManager.Unarchive(PathLocator.Files.PowPlan, resourceData);
                            }

                            string _guid = Guid.NewGuid().ToString("D");

                            using (_powercfg)
                            {
                                _powercfg.StartInfo.Arguments = $@"-import ""{PathLocator.Files.PowPlan}"" {_guid}";
                                _powercfg.Start();

                                await Task.Delay(5);

                                _powercfg.StartInfo.Arguments = $"/setactive {_guid}";
                                _powercfg.Start();

                                _powercfg.StartInfo.Arguments = unlockFrequency;
                                _powercfg.Start();
                            }

                            _currentPowerGuid = _guid;
                            await CommandExecutor.RunCommand($"/c timeout /t 10 && rd /s /q \"{PathLocator.Folders.Workspace}\"");
                        }
                    }
                    else
                    {
                        string activeScheme = @"Microsoft:PowerPlan\\{" + RegistryHelp.GetValue($@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{searchScheme}", "ActivePowerScheme", string.Empty) + "}";
                        string selectedScheme = string.Empty, backupScheme = string.Empty;

                        foreach (var managementObj in new ManagementObjectSearcher(@"root\cimv2\power", "SELECT InstanceID FROM Win32_PowerPlan WHERE InstanceID !='" + activeScheme + "'").Get())
                        {
                            string instanceId = Convert.ToString(managementObj["InstanceID"]) ?? string.Empty;

                            var match = Regex.Match(instanceId, @"\{([a-fA-F0-9\-]{36})\}");
                            searchScheme = match.Groups[1].Value;

                            if (!RegistryHelp.GetValue($@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{searchScheme}", "Description", string.Empty).Contains("-10") &&
                            !RegistryHelp.GetValue($@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{searchScheme}", "FriendlyName", string.Empty).Contains("-11"))
                            {
                                selectedScheme = searchScheme;
                                break;
                            }

                            backupScheme ??= searchScheme;
                        }

                        selectedScheme ??= backupScheme;

                        if (!string.IsNullOrEmpty(selectedScheme))
                        {
                            _currentPowerGuid = selectedScheme;

                            using (_powercfg)
                            {
                                _powercfg.StartInfo.Arguments = $"/setactive {selectedScheme}";
                                _powercfg.Start();
                            }
                        }
                    }
                }
                catch (Exception ex) { ErrorLogging.LogDebug(ex); }
            });
        }
    }
}
