using System.IO;
using System.Management;
using System.Threading;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
using NetFwTypeLib;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Controls;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class SecurityPage : Page
{
    private DispatcherTimer? _refreshTimer;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isCheckInProgress;
    private string? _pendingScrollTarget;

    public SecurityPage()
    {
        InitializeComponent();

        _cancellationTokenSource = new CancellationTokenSource();

        _ = CheckSecurityStatusAsync(_cancellationTokenSource.Token);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _refreshTimer.Tick += async (s, e) => await CheckSecurityStatusAsync(_cancellationTokenSource.Token);
        _refreshTimer.Start();

        Unloaded += (s, e) =>
        {
            _refreshTimer?.Stop();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        };

        Loaded += SecurityPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string optionTag && !string.IsNullOrEmpty(optionTag))
        {
            _pendingScrollTarget = optionTag;
        }
    }

    private async void SecurityPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_pendingScrollTarget))
        {
            await ScrollToElementHelper.ScrollToElementAsync(this, _pendingScrollTarget);
            _pendingScrollTarget = null;
        }
    }

    private async Task CheckSecurityStatusAsync(CancellationToken cancellationToken = default)
    {
        if (_isCheckInProgress)
            return;

        _isCheckInProgress = true;

        try
        {
            var checksTask = Task.Run(async () =>
            {
                var antivirusInfo = await GetAntivirusInfoAsync(cancellationToken).ConfigureAwait(false);
                var firewallProtection = await IsFirewallEnabledAsync(cancellationToken).ConfigureAwait(false);
                var windowsUpdate = await IsWindowsUpdateEnabledAsync(cancellationToken).ConfigureAwait(false);
                var smartscreen = await IsSmartScreenEnabledAsync(cancellationToken).ConfigureAwait(false);
                var realTimeProtection = await IsRealTimeProtectionEnabledAsync(cancellationToken).ConfigureAwait(false);
                var uac = await IsUACEnabledAsync(cancellationToken).ConfigureAwait(false);
                var tamperProtection = await IsTamperProtectionEnabledAsync(cancellationToken).ConfigureAwait(false);
                var controlledFolderAccess = await IsControlledFolderAccessEnabledAsync(cancellationToken).ConfigureAwait(false);
                var bitLockerEnabled = await IsBitLockerEnabledAsync(cancellationToken).ConfigureAwait(false);
                var defenderServiceEnabled = await IsDefenderServiceEnabledAsync(cancellationToken).ConfigureAwait(false);

                return (antivirusInfo, firewallProtection, windowsUpdate, smartscreen, realTimeProtection,
                        uac, tamperProtection, controlledFolderAccess, bitLockerEnabled, defenderServiceEnabled);
            }, cancellationToken);

            var results = await checksTask.ConfigureAwait(true);

            DispatcherQueue.TryEnqueue(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                UpdateStatusCard(VirusThreatProtectionStatus, VirusThreatProtectionLink, results.antivirusInfo.IsEnabled);
                UpdateStatusCard(FirewallStatus, FirewallLink, results.firewallProtection);
                UpdateStatusCard(WindowsUpdateStatus, WindowsUpdateLink, results.windowsUpdate);
                UpdateStatusCard(SmartScreenStatus, SmartScreenLink, results.smartscreen);
                UpdateStatusCard(RealTimeProtectionStatus, RealTimeProtectionLink, results.realTimeProtection);
                UpdateStatusCard(UACStatus, UACLink, results.uac);
                UpdateStatusCard(TamperProtectionStatus, TamperProtectionLink, results.tamperProtection);
                UpdateStatusCard(ControlledFolderAccessStatus, ControlledFolderAccessLink, results.controlledFolderAccess);
                UpdateStatusCard(BitLockerStatus, BitLockerLink, results.bitLockerEnabled);
                UpdateStatusCard(DefenderServiceStatus, DefenderServiceLink, results.defenderServiceEnabled);

                AntivirusProductName.Text = results.antivirusInfo.ProductName ?? ResourceString.GetString("None");

                if (results.antivirusInfo.SignatureUpdated.HasValue)
                {
                    SignatureUpdateText.Text = $"{ResourceString.GetString("SecurityPage_LastUpdated")}: {results.antivirusInfo.SignatureUpdated.Value:g}";
                    SignatureUpdateText.Visibility = Visibility.Visible;
                }
                else
                {
                    SignatureUpdateText.Visibility = Visibility.Collapsed;
                }

                UpdateSecurityImage(results.antivirusInfo.IsEnabled, results.firewallProtection, results.windowsUpdate,
                    results.smartscreen, results.uac, results.realTimeProtection, results.tamperProtection, results.defenderServiceEnabled);

                LastRefreshedText.Text = $"{ResourceString.GetString("SecurityPage_LastRefreshed")}: {DateTime.Now:T}";
            });
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
        }
        finally
        {
            _isCheckInProgress = false;
        }
    }

    private void UpdateStatusCard(TextBlock statusText, HyperlinkButton link, bool isEnabled)
    {
        statusText.Text = isEnabled ? ResourceString.GetString("Enabled") : ResourceString.GetString("Disabled");
        link.Visibility = isEnabled ? Visibility.Collapsed : Visibility.Visible;
    }

    [Flags]
    enum ProductState
    {
        Off = 0x0000,
        On = 0x1000,
        Snoozed = 0x2000,
        Expired = 0x3000
    }

    private class AntivirusInfo
    {
        public string? ProductName { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime? SignatureUpdated { get; set; }
    }

    private async Task<AntivirusInfo> GetAntivirusInfoAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var result = new AntivirusInfo { ProductName = "Windows Defender", IsEnabled = false };

            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT * FROM AntiVirusProduct");

                ManagementObjectCollection? products;
                try { products = searcher.Get(); }
                catch (ManagementException) { products = null; }

                if (products != null && products.Count > 0)
                {
                    foreach (var obj in products)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        var productName = obj["displayName"]?.ToString();

                        if (obj["productState"] != null && int.TryParse(obj["productState"].ToString(), out var state))
                        {
                            var productState = (ProductState)(state & 0xF000);
                            var isEnabled = productState == ProductState.On;

                            if (isEnabled || result.ProductName == "Windows Defender")
                            {
                                result.ProductName = productName ?? "Unknown Antivirus";
                                result.IsEnabled = isEnabled;
                            }
                        }
                    }
                }

                try
                {
                    using var defenderSearcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Defender",
                        "SELECT * FROM MSFT_MpComputerStatus");
                    foreach (var obj in defenderSearcher.Get())
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        if (obj["AntivirusSignatureLastUpdated"] != null)
                        {
                            result.SignatureUpdated = ManagementDateTimeConverter.ToDateTime(obj["AntivirusSignatureLastUpdated"].ToString());
                        }
                        break;
                    }
                }
                catch (ManagementException) { }
                catch { }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }

            return result;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsFirewallEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
                if (type != null && Activator.CreateInstance(type) is INetFwPolicy2 firewallPolicy)
                {
                    return firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN] ||
                           firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] ||
                           firewallPolicy.FirewallEnabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC];
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            return false;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsWindowsUpdateEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key1 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU");
                if (key1?.GetValue("NoAutoUpdate") is int noAutoUpdate && noAutoUpdate == 1)
                    return false;

                using var key2 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate");
                if (key2?.GetValue("DisableWindowsUpdateAccess") is int disabled && disabled == 1)
                    return false;

                using var key3 = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\wuauserv");
                if (key3?.GetValue("Start") is int start && start == 4)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            return false;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsSmartScreenEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var policyKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\System");
                if (policyKey?.GetValue("EnableSmartScreen") is int policyValue && policyValue == 0)
                {
                    return false;
                }

                using var explorerKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer");
                var smartScreenValue = explorerKey?.GetValue("SmartScreenEnabled") as string;

                if (smartScreenValue == "Off")
                    return false;

                using var userExplorerKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer");
                var userSmartScreenValue = userExplorerKey?.GetValue("SmartScreenEnabled") as string;

                if (userSmartScreenValue == "Off")
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsRealTimeProtectionEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection");
                var value = key?.GetValue("DisableRealtimeMonitoring");

                if (value == null)
                    return true;

                return (int)value == 0;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            return false;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsUACEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
                var value = key?.GetValue("EnableLUA");
                return value is int enabled && enabled == 1;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            return false;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsTamperProtectionEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Features");
                var value = key?.GetValue("TamperProtection");

                if (value == null)
                {
                    return true;
                }

                return (int)value == 5;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            return false;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsControlledFolderAccessEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
        {
            try
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -NonInteractive -Command \"(Get-MpPreference).EnableControlledFolderAccess\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                        if (int.TryParse(output.Trim(), out var status))
                        {
                            return status != 0;
                        }
                    }
                }
                catch (Exception psEx)
                {
                    ErrorLogging.LogDebug(psEx);
                }

                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Windows Defender Exploit Guard\Controlled Folder Access");
                if (key != null)
                {
                    var value = key.GetValue("EnableControlledFolderAccess");

                    if (value != null)
                    {
                        var status = (int)value;
                        return status != 0;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            return false;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsBitLockerEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var drives = DriveInfo.GetDrives();
                foreach (var drive in drives)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (drive.DriveType == DriveType.Fixed)
                    {
                        try
                        {
                            using var searcher = new ManagementObjectSearcher(@"root\CIMV2\Security\MicrosoftVolumeEncryption",
                                $"SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter = '{drive.Name.TrimEnd('\\', ':')}'");

                            ManagementObjectCollection? volumes;
                            try { volumes = searcher.Get(); }
                            catch (ManagementException) { volumes = null; }

                            if (volumes != null)
                            {
                                foreach (var volume in volumes)
                                {
                                    var protectionStatus = volume["ProtectionStatus"];
                                    if (protectionStatus != null && (uint)protectionStatus == 1)
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            return false;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsDefenderServiceEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\WinDefend");
                if (key?.GetValue("Start") is int startType)
                {
                    return startType != 4;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            return false;
        }, cancellationToken).ConfigureAwait(false);
    }

    private void UpdateSecurityImage(params bool[] featureStates)
    {
        var disabledCount = featureStates.Count(status => !status);

        var imageUri = disabledCount switch
        {
            0 => "ms-appx:///Assets/PngImages/Secure.png",
            <= 2 => "ms-appx:///Assets/PngImages/Warning.png",
            _ => "ms-appx:///Assets/PngImages/UnSecure.png"
        };

        SecurityStatusImage.Source = new BitmapImage(new Uri(imageUri));

        SecurityStatusLoadingRing.IsActive = false;
        SecurityStatusLoadingRing.Visibility = Visibility.Collapsed;
        SecurityStatusImage.Visibility = Visibility.Visible;
        LastRefreshedText.Visibility = Visibility.Visible;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
    }

    private void OpenWindowsSecurity_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "windowsdefender://",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
        }
    }

    private async void RunQuickScan_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            QuickScanButton.IsEnabled = false;
            QuickScanProgressRing.Visibility = Visibility.Visible;
            QuickScanIcon.Visibility = Visibility.Collapsed;

            await Task.Run(() =>
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Start-MpScan -ScanType QuickScan\"",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }
                };
                process.Start();
                process.WaitForExit();
            }).ConfigureAwait(true);

            // App.ShowNotification(ResourceString.GetString("SecurityPage_QuickScanTitle"), ResourceString.GetString("SecurityPage_QuickScanCompleted"), Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success, 5000);

            await Task.Delay(1000);
            await CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            // App.ShowNotification(ResourceString.GetString("SecurityPage_QuickScanTitle"), ResourceString.GetString("SecurityPage_QuickScanFailed"), Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error, 5000);
        }
        finally
        {
            QuickScanButton.IsEnabled = true;
            QuickScanProgressRing.Visibility = Visibility.Collapsed;
            QuickScanIcon.Visibility = Visibility.Visible;
        }
    }

    private async void UpdateDefenderSignatures_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await Task.Run(() =>
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Update-MpSignature\"",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }
                };
                process.Start();
                process.WaitForExit();
            }).ConfigureAwait(true);

            // App.ShowNotification(ResourceString.GetString("SecurityPage_UpdateDefinitionsTitle"), ResourceString.GetString("SecurityPage_DefinitionsUpdated"), Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success, 5000);

            await Task.Delay(2000);
            await CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            // App.ShowNotification(ResourceString.GetString("SecurityPage_UpdateDefinitionsTitle"), ResourceString.GetString("SecurityPage_DefinitionsUpdateFailed"), Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error, 5000);
        }
    }

    private void VirusThreatProtectionLink_Click(object sender, RoutedEventArgs e)
    {
        OpenWindowsSecurityPage("windowsdefender://threatsettings/");
    }

    private void FirewallLink_Click(object sender, RoutedEventArgs e)
    {
        OpenWindowsSecurityPage("windowsdefender://network/");
    }

    private void WindowsUpdateLink_Click(object sender, RoutedEventArgs e)
    {
        OpenWindowsSecurityPage("ms-settings:windowsupdate");
    }

    private void SmartScreenLink_Click(object sender, RoutedEventArgs e)
    {
        OpenWindowsSecurityPage("windowsdefender://smartscreenpua/");
    }

    private void RealTimeProtectionLink_Click(object sender, RoutedEventArgs e)
    {
        OpenWindowsSecurityPage("windowsdefender://threatsettings/");
    }

    private void UACLink_Click(object sender, RoutedEventArgs e)
    {
        OpenWindowsSecurityPage("ms-settings:useraccounts");
    }

    private void TamperProtectionLink_Click(object sender, RoutedEventArgs e)
    {
        OpenWindowsSecurityPage("windowsdefender://threatsettings/");
    }

    private void ControlledFolderAccessLink_Click(object sender, RoutedEventArgs e)
    {
        OpenWindowsSecurityPage("windowsdefender://ransomwareprotection/");
    }

    private void BitLockerLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:deviceencryption",
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "control.exe",
                    Arguments = "/name Microsoft.BitLockerDriveEncryption",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
        }
    }

    private void DefenderServiceLink_Click(object sender, RoutedEventArgs e)
    {
        OpenWindowsSecurityPage("windowsdefender://threatsettings/");
    }

    private void OpenWindowsSecurityPage(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "windowsdefender://",
                    UseShellExecute = true
                });
            }
            catch (Exception fallbackEx)
            {
                ErrorLogging.LogDebug(fallbackEx);
            }
        }
    }
}