// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Threading;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class SecurityPage : Page, IPurgeable
{
    #region Fields
    private DispatcherTimer? _refreshTimer;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isCheckInProgress;
    private string? _pendingScrollTarget;
    private bool _isUacSliderUpdating = false;
    private bool _isSmartAppControlUpdating = false;
    private bool _isPowerShellPolicyUpdating = false;
    private bool _isRdpToggleUpdating = false;
    private bool _isRaToggleUpdating = false;
    private bool _isDevModeToggleUpdating = false;
    #endregion

    #region Constructor & Lifecycle
    public SecurityPage()
    {
        InitializeComponent();

        _cancellationTokenSource = new CancellationTokenSource();

        _ = CheckSecurityStatusAsync(_cancellationTokenSource.Token);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };

        _refreshTimer.Tick += async (s, e) =>
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                await CheckSecurityStatusAsync(_cancellationTokenSource.Token);
            }
        };
        _refreshTimer.Start();

        Loaded += SecurityPage_Loaded;
        Unloaded += SecurityPage_Unloaded;
    }

    private void SecurityPage_Unloaded(object sender, RoutedEventArgs e)
    {
        Purge();
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

        LastRefreshedText.Text = string.Empty; ;
    }
    #endregion

    #region Core Diagnostics
    private async Task CheckSecurityStatusAsync(CancellationToken cancellationToken = default)
    {
        if (_isCheckInProgress || cancellationToken.IsCancellationRequested)
            return;

        _isCheckInProgress = true;

        try
        {
            TxtSecurityStatus.Text = ResourceString.GetString("text_scanning_system") ?? "Scanning...";

            var results = await Task.Run(async () =>
            {
                var antivirusInfo = await SecurityDiagnostics.GetAntivirusInfoAsync(cancellationToken).ConfigureAwait(false);
                var firewallProtection = await SecurityDiagnostics.IsFirewallEnabledAsync(cancellationToken).ConfigureAwait(false);
                var windowsUpdate = await SecurityDiagnostics.IsWindowsUpdateEnabledAsync(cancellationToken).ConfigureAwait(false);
                var smartscreen = await SecurityDiagnostics.IsSmartScreenEnabledAsync(cancellationToken).ConfigureAwait(false);
                var realTimeProtection = await SecurityDiagnostics.IsRealTimeProtectionEnabledAsync(cancellationToken).ConfigureAwait(false);
                var uac = await SecurityDiagnostics.IsUACEnabledAsync(cancellationToken).ConfigureAwait(false);
                var tamperProtection = await SecurityDiagnostics.IsTamperProtectionEnabledAsync(cancellationToken).ConfigureAwait(false);
                var controlledFolderAccess = await SecurityDiagnostics.IsControlledFolderAccessEnabledAsync(cancellationToken).ConfigureAwait(false);
                var bitLockerEnabled = await SecurityDiagnostics.IsBitLockerEnabledAsync(cancellationToken).ConfigureAwait(false);
                var coreIsolationEnabled = await SecurityDiagnostics.IsCoreIsolationEnabledAsync(cancellationToken).ConfigureAwait(false);
                var defenderServiceEnabled = await SecurityDiagnostics.IsDefenderServiceEnabledAsync(cancellationToken).ConfigureAwait(false);
                var accountProtectionEnabled = await SecurityDiagnostics.IsAccountProtectionEnabledAsync(cancellationToken).ConfigureAwait(false);
                var smartAppControlState = await SecurityDiagnostics.GetSmartAppControlStateAsync(cancellationToken).ConfigureAwait(false);
                var psPolicy = await SecurityDiagnostics.GetPowerShellExecutionPolicyAsync(cancellationToken).ConfigureAwait(false);
                var lsaProtection = await SecurityDiagnostics.IsLsaProtectionEnabledAsync(cancellationToken).ConfigureAwait(false);
                var rdpEnabled = await SecurityDiagnostics.IsRdpEnabledAsync(cancellationToken).ConfigureAwait(false);
                var raEnabled = await SecurityDiagnostics.IsRemoteAssistanceEnabledAsync(cancellationToken).ConfigureAwait(false);
                var devModeEnabled = await SecurityDiagnostics.IsDeveloperModeEnabledAsync(cancellationToken).ConfigureAwait(false);

                return (antivirusInfo, firewallProtection, windowsUpdate, smartscreen, realTimeProtection,
                        uac, tamperProtection, controlledFolderAccess, bitLockerEnabled, coreIsolationEnabled,
                        defenderServiceEnabled, accountProtectionEnabled, smartAppControlState, psPolicy, lsaProtection, rdpEnabled, raEnabled, devModeEnabled);
            }, cancellationToken).ConfigureAwait(true);

            if (cancellationToken.IsCancellationRequested || this.XamlRoot == null)
                return;

            UpdateStatusCard(VirusThreatProtectionStatus, VirusThreatProtectionLink, results.antivirusInfo.IsEnabled);
            UpdateStatusCard(FirewallStatus, FirewallLink, results.firewallProtection);
            UpdateStatusCard(WindowsUpdateStatus, WindowsUpdateLink, results.windowsUpdate);
            UpdateStatusCard(SmartScreenStatus, SmartScreenLink, results.smartscreen);
            UpdateStatusCard(CoreIsolationStatus, CoreIsolationLink, results.coreIsolationEnabled);
            UpdateStatusCard(RealTimeProtectionStatus, RealTimeProtectionLink, results.realTimeProtection);
            UpdateStatusCard(AccountProtectionStatus, AccountProtectionLink, results.accountProtectionEnabled);
            UpdateStatusCard(LsaProtectionStatus, LsaProtectionLink, results.lsaProtection);
            UpdateStatusCard(TamperProtectionStatus, TamperProtectionLink, results.tamperProtection);
            UpdateStatusCard(ControlledFolderAccessStatus, ControlledFolderAccessLink, results.controlledFolderAccess);
            UpdateStatusCard(BitLockerStatus, BitLockerLink, results.bitLockerEnabled);
            UpdateStatusCard(DefenderServiceStatus, DefenderServiceLink, results.defenderServiceEnabled);

            RemoteDesktopStatus.Text = results.rdpEnabled ? ResourceString.GetString("Enabled") : ResourceString.GetString("Disabled");
            if (RemoteDesktopLink != null) RemoteDesktopLink.Visibility = Visibility.Collapsed;
            _isRdpToggleUpdating = true;
            RdpToggleSwitch.IsOn = results.rdpEnabled;
            RdpToggleSwitch.IsEnabled = true;
            _isRdpToggleUpdating = false;

            RemoteAssistanceStatus.Text = results.raEnabled ? ResourceString.GetString("Enabled") : ResourceString.GetString("Disabled");
            if (RemoteAssistanceLink != null) RemoteAssistanceLink.Visibility = Visibility.Collapsed;
            _isRaToggleUpdating = true;
            RaToggleSwitch.IsOn = results.raEnabled;
            RaToggleSwitch.IsEnabled = true;
            _isRaToggleUpdating = false;

            DeveloperModeStatus.Text = results.devModeEnabled ? ResourceString.GetString("Enabled") : ResourceString.GetString("Disabled");
            if (DeveloperModeLink != null) DeveloperModeLink.Visibility = Visibility.Collapsed;
            _isDevModeToggleUpdating = true;
            DeveloperModeToggleSwitch.IsOn = results.devModeEnabled;
            DeveloperModeToggleSwitch.IsEnabled = true;
            _isDevModeToggleUpdating = false;

            _isUacSliderUpdating = true;
            UacSlider.IsEnabled = true;
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
                int consentBehavior = (int)(key?.GetValue("ConsentPromptBehaviorAdmin") ?? 5);
                int secureDesktop = (int)(key?.GetValue("PromptOnSecureDesktop") ?? 1);

                if (consentBehavior == 2 && secureDesktop == 1)
                {
                    UacSlider.Value = 3;
                    UacLevelDescription.Text = ResourceString.GetString("UAC_Level3") ?? "Always notify me";
                }
                else if (consentBehavior == 5 && secureDesktop == 1)
                {
                    UacSlider.Value = 2;
                    UacLevelDescription.Text = ResourceString.GetString("UAC_Level2") ?? "Notify me only when apps try to make changes (default)";
                }
                else if (consentBehavior == 5 && secureDesktop == 0)
                {
                    UacSlider.Value = 1;
                    UacLevelDescription.Text = ResourceString.GetString("UAC_Level1") ?? "Notify me only when apps try to make changes (do not dim desktop)";
                }
                else
                {
                    UacSlider.Value = 0;
                    UacLevelDescription.Text = ResourceString.GetString("UAC_Level0") ?? "Never notify me (Not recommended)";
                }
            }
            catch
            {
                UacSlider.IsEnabled = false;
                UacLevelDescription.Text = "Access denied reading UAC status.";
            }
            _isUacSliderUpdating = false;

            _isSmartAppControlUpdating = true;
            bool isSmartAppControlSecure = results.smartAppControlState != 0;
            if (results.smartAppControlState == -1)
            {
                SmartAppControlComboBox.IsEnabled = false;
                SmartAppControlDescription.Text = "Access denied reading Smart App Control status.";
            }
            else
            {
                SmartAppControlComboBox.IsEnabled = true;
                if (results.smartAppControlState == 0)
                {
                    SmartAppControlComboBox.SelectedIndex = 0;
                    SmartAppControlDescription.Text = ResourceString.GetString("SmartAppControl_Level0") ?? "Smart App Control is off.";
                }
                else if (results.smartAppControlState == 1)
                {
                    SmartAppControlComboBox.SelectedIndex = 2;
                    SmartAppControlDescription.Text = ResourceString.GetString("SmartAppControl_Level1") ?? "Smart App Control is on and enforcing protection.";
                }
                else
                {
                    SmartAppControlComboBox.SelectedIndex = 1;
                    SmartAppControlDescription.Text = ResourceString.GetString("SmartAppControl_Level2") ?? "Evaluating if Smart App Control can protect you without getting in the way.";
                }
            }
            _isSmartAppControlUpdating = false;

            _isPowerShellPolicyUpdating = true;
            bool isPsWarning = false;
            if (results.psPolicy == "Error")
            {
                PowerShellPolicyComboBox.IsEnabled = false;
                PowerShellPolicyDescription.Text = "Access denied reading PowerShell Execution Policy.";
            }
            else
            {
                PowerShellPolicyComboBox.IsEnabled = true;
                switch (results.psPolicy)
                {
                    case "Restricted":
                        PowerShellPolicyComboBox.SelectedIndex = 0;
                        PowerShellPolicyDescription.Text = ResourceString.GetString("text_ps_policy_restricted") ?? "Only individual commands are allowed.";
                        break;
                    case "AllSigned":
                        PowerShellPolicyComboBox.SelectedIndex = 1;
                        PowerShellPolicyDescription.Text = ResourceString.GetString("text_ps_policy_allsigned") ?? "Only scripts signed by a trusted publisher can run.";
                        break;
                    case "RemoteSigned":
                        PowerShellPolicyComboBox.SelectedIndex = 2;
                        PowerShellPolicyDescription.Text = ResourceString.GetString("text_ps_policy_remotesigned") ?? "Local scripts allowed; downloaded scripts must be signed.";
                        break;
                    case "Unrestricted":
                        PowerShellPolicyComboBox.SelectedIndex = 3;
                        PowerShellPolicyDescription.Text = $"⚠️ {ResourceString.GetString("text_ps_policy_unrestricted") ?? "All scripts allowed with a warning for internet files."}";
                        isPsWarning = true;
                        break;
                    case "Bypass":
                        PowerShellPolicyComboBox.SelectedIndex = 4;
                        PowerShellPolicyDescription.Text = $"⚠️ {ResourceString.GetString("text_ps_policy_bypass") ?? "All scripts allowed to run without warnings or blocks."}";
                        isPsWarning = true;
                        break;
                    default:
                        PowerShellPolicyComboBox.SelectedIndex = 0;
                        PowerShellPolicyDescription.Text = "Unknown policy. Defaulting to Restricted UI state.";
                        break;
                }

                if (isPsWarning)
                {
                    PowerShellPolicyDescription.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
                    PowerShellPolicyDescription.Opacity = 1.0;
                }
                else
                {
                    PowerShellPolicyDescription.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
                    PowerShellPolicyDescription.Opacity = 0.8;
                }
            }
            _isPowerShellPolicyUpdating = false;

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

            int issuesCount = 0;
            if (!results.antivirusInfo.IsEnabled) issuesCount++;
            if (!results.firewallProtection) issuesCount++;
            if (!results.realTimeProtection) issuesCount++;
            if (!results.uac) issuesCount++;
            if (!results.windowsUpdate) issuesCount++;
            if (!results.tamperProtection) issuesCount++;
            if (!isSmartAppControlSecure) issuesCount++;
            if (!results.lsaProtection) issuesCount++;
            if (results.rdpEnabled) issuesCount++;
            if (results.raEnabled) issuesCount++;
            if (results.devModeEnabled) issuesCount++;

            bool isPsPolicySecure = results.psPolicy != "Unrestricted" && results.psPolicy != "Bypass" && results.psPolicy != "Error";
            if (!isPsPolicySecure) issuesCount++;

            bool isCoreProtected = results.antivirusInfo.IsEnabled &&
                                   results.firewallProtection &&
                                   results.realTimeProtection;

            string imageUri;
            string statusText;

            if (!isCoreProtected)
            {
                imageUri = "ms-appx:///Assets/PngImages/UnSecure.png";
                statusText = $"{issuesCount} {ResourceString.GetString("text_security_critical") ?? "Critical Issues"}";
            }
            else if (issuesCount > 0)
            {
                imageUri = "ms-appx:///Assets/PngImages/Secure.png";
                statusText = $"{issuesCount} {ResourceString.GetString("text_security_warning") ?? "Warnings Found"}";
            }
            else
            {
                imageUri = "ms-appx:///Assets/PngImages/Secure.png";
                statusText = ResourceString.GetString("text_security_good") ?? "System is Secure";
            }

            SecurityStatusImage.Source = new BitmapImage(new Uri(imageUri));
            TxtSecurityStatus.Text = statusText;

            SecurityStatusLoadingRing.IsActive = false;
            SecurityStatusLoadingRing.Visibility = Visibility.Collapsed;
            SecurityStatusImage.Visibility = Visibility.Visible;
            TxtSecurityStatus.Visibility = Visibility.Visible;

            LastRefreshedText.Text = $"{ResourceString.GetString("SecurityPage_LastRefreshed")}: {DateTime.Now:T}";
            LastRefreshedText.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            // Silent exit
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            TxtSecurityStatus.Text = "Scan failed.";
        }
        finally
        {
            _isCheckInProgress = false;
        }
    }
    #endregion

    #region UI Update Helpers
    private void UpdateStatusCard(TextBlock statusText, HyperlinkButton link, bool isEnabled)
    {
        statusText.Text = isEnabled ? ResourceString.GetString("Enabled") : ResourceString.GetString("Disabled");
        link.Visibility = isEnabled ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateSecurityImage(params bool[] featureStates)
    {
        if (SecurityStatusImage == null) return;

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
    #endregion

    #region Top Bar Actions
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

            App.ShowNotification(ResourceString.GetString("SecurityPage_QuickScanTitle"), ResourceString.GetString("SecurityPage_QuickScanCompleted"), InfoBarSeverity.Success, 5000);

            await Task.Delay(1000);
            await CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            App.ShowNotification(ResourceString.GetString("SecurityPage_QuickScanTitle"), ResourceString.GetString("SecurityPage_QuickScanFailed"), InfoBarSeverity.Error, 5000);
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

            App.ShowNotification(ResourceString.GetString("SecurityPage_UpdateDefinitionsTitle"), ResourceString.GetString("SecurityPage_DefinitionsUpdated"), InfoBarSeverity.Success, 5000);

            await Task.Delay(2000);
            await CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            App.ShowNotification(ResourceString.GetString("SecurityPage_UpdateDefinitionsTitle"), ResourceString.GetString("SecurityPage_DefinitionsUpdateFailed"), InfoBarSeverity.Error, 5000);
        }
    }
    #endregion

    #region Security Card Links
    private void VirusThreatProtectionLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://threatsettings/");
    private void FirewallLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://network/");
    private void WindowsUpdateLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("ms-settings:windowsupdate");
    private void SmartScreenLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://smartscreenpua/");
    private void RealTimeProtectionLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://threatsettings/");
    private void TamperProtectionLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://threatsettings/");
    private void CoreIsolationLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://coreisolation/");
    private void ControlledFolderAccessLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://ransomwareprotection/");
    private void AccountProtectionLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://account/");
    private void DefenderServiceLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://threatsettings/");
    private void SmartAppControlLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://smartapp/");
    private void LsaProtectionLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://coreisolation/");
    private void RemoteDesktopLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("ms-settings:remotedesktop");

    private void RemoteAssistanceLink_Click(object sender, RoutedEventArgs e)
    {
        if (!RaToggleSwitch.IsOn)
        {
            App.ShowNotification(
                ResourceString.GetString("SecurityPage_RemoteAssistance") ?? "Remote Assistance",
                ResourceString.GetString("SecurityPage_RemoteAssistanceDisabledWarning") ?? "Remote Assistance must be enabled to launch this tool.",
                InfoBarSeverity.Warning,
                3000);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("msra.exe") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
        }
    }

    private void DeveloperModeLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:developers") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
        }
    }

    private void BitLockerLink_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo { FileName = "ms-settings:deviceencryption", UseShellExecute = true }); }
        catch
        {
            try { Process.Start(new ProcessStartInfo { FileName = "control.exe", Arguments = "/name Microsoft.BitLockerDriveEncryption", UseShellExecute = true }); }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); }
        }
    }

    private void UacCard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("UserAccountControlSettings.exe") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
        }
    }

    private void UacSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUacSliderUpdating) return;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
            if (key != null)
            {
                // Level 3: Always Notify
                if (e.NewValue == 3)
                {
                    key.SetValue("ConsentPromptBehaviorAdmin", 2, RegistryValueKind.DWord);
                    key.SetValue("PromptOnSecureDesktop", 1, RegistryValueKind.DWord);
                    UacLevelDescription.Text = ResourceString.GetString("UAC_Level3") ?? "Always notify me";
                }
                // Level 2: Default
                else if (e.NewValue == 2)
                {
                    key.SetValue("ConsentPromptBehaviorAdmin", 5, RegistryValueKind.DWord);
                    key.SetValue("PromptOnSecureDesktop", 1, RegistryValueKind.DWord);
                    UacLevelDescription.Text = ResourceString.GetString("UAC_Level2") ?? "Notify me only when apps try to make changes (default)";
                }
                // Level 1: Don't dim desktop
                else if (e.NewValue == 1)
                {
                    key.SetValue("ConsentPromptBehaviorAdmin", 5, RegistryValueKind.DWord);
                    key.SetValue("PromptOnSecureDesktop", 0, RegistryValueKind.DWord);
                    UacLevelDescription.Text = ResourceString.GetString("UAC_Level1") ?? "Notify me only when apps try to make changes (do not dim desktop)";
                }
                // Level 0: Never notify
                else if (e.NewValue == 0)
                {
                    key.SetValue("ConsentPromptBehaviorAdmin", 0, RegistryValueKind.DWord);
                    key.SetValue("PromptOnSecureDesktop", 0, RegistryValueKind.DWord);
                    UacLevelDescription.Text = ResourceString.GetString("UAC_Level0") ?? "Never notify me (Not recommended)";
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);

            _isUacSliderUpdating = true;
            UacSlider.Value = e.OldValue;
            _isUacSliderUpdating = false;
        }
    }

    private void SmartAppControlComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSmartAppControlUpdating) return;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CI\Policy", true);
            if (key != null)
            {
                int selectedIndex = SmartAppControlComboBox.SelectedIndex;
                int regValue = 2; // Default to Eval

                // Index 0: Off
                if (selectedIndex == 0)
                {
                    regValue = 0;
                    SmartAppControlDescription.Text = ResourceString.GetString("SmartAppControl_Level0") ?? "Smart App Control is off.";
                }
                // Index 1: Evaluation Mode
                else if (selectedIndex == 1)
                {
                    regValue = 2;
                    SmartAppControlDescription.Text = ResourceString.GetString("SmartAppControl_Level2") ?? "Evaluating if Smart App Control can protect you without getting in the way.";
                }
                // Index 2: On (Enforced)
                else if (selectedIndex == 2)
                {
                    regValue = 1;
                    SmartAppControlDescription.Text = ResourceString.GetString("SmartAppControl_Level1") ?? "Smart App Control is on and enforcing protection.";
                }

                key.SetValue("VerifiedAndReputablePolicyState", regValue, RegistryValueKind.DWord);
            }
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);

            _ = CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
        }
    }

    private async void PowerShellPolicyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isPowerShellPolicyUpdating) return;

        int selectedIndex = PowerShellPolicyComboBox.SelectedIndex;

        if (selectedIndex == 3 || selectedIndex == 4)
        {
            ContentDialog warningDialog = new ContentDialog
            {
                Title = ResourceString.GetString("Dialog_SecurityWarningTitle") ?? "Security Warning",
                Content = ResourceString.GetString("Dialog_PSPolicyWarningDesc") ?? "Lowering this policy allows potentially dangerous scripts to run without warnings. Are you sure you want to proceed?",
                PrimaryButtonText = ResourceString.GetString("Dialog_YesProceed") ?? "Yes, change it",
                CloseButtonText = ResourceString.GetString("Dialog_Cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await warningDialog.ShowAsync();

            if (result != ContentDialogResult.Primary)
            {
                _ = CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
                return;
            }
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell", true);
            if (key != null)
            {
                string policy = "Restricted";
                string desc = "";
                bool isWarning = false;

                switch (selectedIndex)
                {
                    case 0:
                        policy = "Restricted";
                        desc = ResourceString.GetString("text_ps_policy_restricted") ?? "Only individual commands are allowed.";
                        break;
                    case 1:
                        policy = "AllSigned";
                        desc = ResourceString.GetString("text_ps_policy_allsigned") ?? "Only scripts signed by a trusted publisher can run.";
                        break;
                    case 2:
                        policy = "RemoteSigned";
                        desc = ResourceString.GetString("text_ps_policy_remotesigned") ?? "Local scripts allowed; downloaded scripts must be signed.";
                        break;
                    case 3:
                        policy = "Unrestricted";
                        desc = $"⚠️ {ResourceString.GetString("text_ps_policy_unrestricted") ?? "All scripts allowed with a warning for internet files."}";
                        isWarning = true;
                        break;
                    case 4:
                        policy = "Bypass";
                        desc = $"⚠️ {ResourceString.GetString("text_ps_policy_bypass") ?? "All scripts allowed to run without warnings or blocks."}";
                        isWarning = true;
                        break;
                }

                key.SetValue("ExecutionPolicy", policy, RegistryValueKind.String);
                PowerShellPolicyDescription.Text = desc;

                if (isWarning)
                {
                    PowerShellPolicyDescription.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
                    PowerShellPolicyDescription.Opacity = 1.0;
                }
                else
                {
                    PowerShellPolicyDescription.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
                    PowerShellPolicyDescription.Opacity = 0.8;

                    App.ShowNotification(
                        ResourceString.GetString("SecurityPage_PSExecutionPolicy") ?? "PowerShell Policy",
                        ResourceString.GetString("text_saved_successfully") ?? "Settings saved securely.",
                        InfoBarSeverity.Success,
                        3000);
                }

                _ = CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
            }
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            _ = CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
        }
    }

    private async void RdpToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isRdpToggleUpdating) return;

        RemoteDesktopStatus.Text = RdpToggleSwitch.IsOn
            ? ResourceString.GetString("Enabled") ?? "Enabled"
            : ResourceString.GetString("Disabled") ?? "Disabled";

        try
        {
            bool enable = RdpToggleSwitch.IsOn;

            await Task.Run(() =>
            {
                int fDenyVal = enable ? 0 : 1;

                string command = $@"
                $ts = Get-WmiObject -Class Win32_TerminalServiceSetting -Namespace root\cimv2\TerminalServices -ComputerName '.' -Authentication 6;
                if ($ts) {{
                    $ts.SetAllowTSConnections({(enable ? 1 : 0)}, 1);
                }}

                $tsPath = 'HKLM:\System\CurrentControlSet\Control\Terminal Server';
                Set-ItemProperty -Path $tsPath -Name 'fDenyTSConnections' -Value {fDenyVal};
                Set-ItemProperty -Path ""$tsPath\WinStations\RDP-Tcp"" -Name 'UserAuthentication' -Value {(enable ? 1 : 0)};
                
                if ({enable.ToString().ToLower()}) {{
                    Enable-NetFirewallRule -DisplayGroup '@{{Microsoft.Windows.RemoteDesktop.RemoteDesktop.Resources.dll,-28752}}';
                }} else {{
                    Disable-NetFirewallRule -DisplayGroup '@{{Microsoft.Windows.RemoteDesktop.RemoteDesktop.Resources.dll,-28752}}';
                }}";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        Verb = "runas"
                    }
                };
                process.Start();
                process.WaitForExit();
            });

            App.ShowNotification(
                ResourceString.GetString("SecurityPage_RemoteDesktop") ?? "Remote Desktop",
                ResourceString.GetString("text_saved_successfully") ?? "Settings synchronized.",
                InfoBarSeverity.Success,
                3000);
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            _isRdpToggleUpdating = true;
            RdpToggleSwitch.IsOn = !RdpToggleSwitch.IsOn;
            _isRdpToggleUpdating = false;
        }

        _ = CheckSecurityStatusAsync(_cancellationTokenSource?.Token ?? default);
    }

    private async void RaToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isRaToggleUpdating) return;

        RemoteAssistanceStatus.Text = RaToggleSwitch.IsOn
            ? ResourceString.GetString("Enabled") ?? "Enabled"
            : ResourceString.GetString("Disabled") ?? "Disabled";

        try
        {
            bool enable = RaToggleSwitch.IsOn;
            await Task.Run(() =>
            {
                int val = enable ? 1 : 0;
                string command = $@"
                Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Remote Assistance' -Name 'fAllowToGetHelp' -Value {val};
                Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server' -Name 'fAllowToGetHelp' -Value {val};
                if ({enable.ToString().ToLower()}) {{
                    Enable-NetFirewallRule -DisplayGroup '@{{FirewallAPI.dll,-28502}}' -ErrorAction SilentlyContinue;
                }} else {{
                    Disable-NetFirewallRule -DisplayGroup '@{{FirewallAPI.dll,-28502}}' -ErrorAction SilentlyContinue;
                }}";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                    // Verb = "runas" */ App is already elevated \*
                };

                Process.Start(psi)?.WaitForExit();
            });

            App.ShowNotification(
                ResourceString.GetString("SecurityPage_RemoteAssistance") ?? "Remote Assistance",
                ResourceString.GetString("text_saved_successfully") ?? "Settings synchronized.",
                InfoBarSeverity.Success,
                3000);
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            _isRaToggleUpdating = true;
            RaToggleSwitch.IsOn = !RaToggleSwitch.IsOn;
            _isRaToggleUpdating = false;
        }

        _ = CheckSecurityStatusAsync();
    }

    private async void DeveloperModeToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isDevModeToggleUpdating) return;

        DeveloperModeStatus.Text = DeveloperModeToggleSwitch.IsOn
            ? ResourceString.GetString("Enabled") ?? "Enabled"
            : ResourceString.GetString("Disabled") ?? "Disabled";

        try
        {
            bool enable = DeveloperModeToggleSwitch.IsOn;

            await Task.Run(() =>
            {
                int val = enable ? 1 : 0;

                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock");
                if (key != null)
                {
                    key.SetValue("AllowAllTrustedApps", val, RegistryValueKind.DWord);
                    key.SetValue("AllowDevelopmentWithoutDevLicense", val, RegistryValueKind.DWord);
                }
            });

            App.ShowNotification(
                ResourceString.GetString("SecurityPage_DeveloperMode") ?? "Developer Mode",
                ResourceString.GetString("text_saved_successfully") ?? "Settings synchronized.",
                InfoBarSeverity.Success,
                3000);
        }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            _isDevModeToggleUpdating = true;
            DeveloperModeToggleSwitch.IsOn = !DeveloperModeToggleSwitch.IsOn;
            _isDevModeToggleUpdating = false;
        }

        _ = CheckSecurityStatusAsync();
    }

    #endregion

    #region Utilities
    private void OpenWindowsSecurityPage(string uri)
    {
        try { Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true }); }
        catch (Exception ex)
        {
            ErrorLogging.LogDebug(ex);
            try { Process.Start(new ProcessStartInfo { FileName = "windowsdefender://", UseShellExecute = true }); }
            catch (Exception fallbackEx) { ErrorLogging.LogDebug(fallbackEx); }
        }
    }

    private async void RefreshTimer_Tick(object? sender, object e)
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            await CheckSecurityStatusAsync(_cancellationTokenSource.Token);
        }
    }
    #endregion

    #region Purge Page
    public void Purge()
    {
        Debug.WriteLine("[SecurityPage] Purge Initiated...");

        if (_refreshTimer != null)
        {
            _refreshTimer.Stop();
            _refreshTimer.Tick -= RefreshTimer_Tick;
            _refreshTimer = null;
            Debug.WriteLine("[SecurityPage] Refresh Timer stopped.");
        }

        if (_cancellationTokenSource != null)
        {
            try
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
            }
            catch (ObjectDisposedException) { }
            _cancellationTokenSource = null;
            Debug.WriteLine("[SecurityPage] Background tasks cancelled.");
        }

        Loaded -= SecurityPage_Loaded;
        Unloaded -= SecurityPage_Unloaded;

        if (this.DataContext is IDisposable disposableVM)
        {
            disposableVM.Dispose();
        }
        this.DataContext = null;

        this.Content = null;

        Debug.WriteLine("[SecurityPage] Purge Complete.");
    }
    #endregion
}