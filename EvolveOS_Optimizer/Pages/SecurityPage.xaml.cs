using System.Threading;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Xaml.Navigation;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class SecurityPage : Page, IPurgeable
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
    }

    private async Task CheckSecurityStatusAsync(CancellationToken cancellationToken = default)
    {
        if (_isCheckInProgress || cancellationToken.IsCancellationRequested)
            return;

        _isCheckInProgress = true;

        try
        {
            var results = await Task.Run(async () =>
            {
                // Hooked up to the new shared SecurityDiagnostics engine!
                var antivirusInfo = await SecurityDiagnostics.GetAntivirusInfoAsync(cancellationToken).ConfigureAwait(false);
                var firewallProtection = await SecurityDiagnostics.IsFirewallEnabledAsync(cancellationToken).ConfigureAwait(false);
                var windowsUpdate = await SecurityDiagnostics.IsWindowsUpdateEnabledAsync(cancellationToken).ConfigureAwait(false);
                var smartscreen = await SecurityDiagnostics.IsSmartScreenEnabledAsync(cancellationToken).ConfigureAwait(false);
                var realTimeProtection = await SecurityDiagnostics.IsRealTimeProtectionEnabledAsync(cancellationToken).ConfigureAwait(false);
                var uac = await SecurityDiagnostics.IsUACEnabledAsync(cancellationToken).ConfigureAwait(false);
                var tamperProtection = await SecurityDiagnostics.IsTamperProtectionEnabledAsync(cancellationToken).ConfigureAwait(false);
                var controlledFolderAccess = await SecurityDiagnostics.IsControlledFolderAccessEnabledAsync(cancellationToken).ConfigureAwait(false);
                var bitLockerEnabled = await SecurityDiagnostics.IsBitLockerEnabledAsync(cancellationToken).ConfigureAwait(false);
                var defenderServiceEnabled = await SecurityDiagnostics.IsDefenderServiceEnabledAsync(cancellationToken).ConfigureAwait(false);

                return (antivirusInfo, firewallProtection, windowsUpdate, smartscreen, realTimeProtection,
                        uac, tamperProtection, controlledFolderAccess, bitLockerEnabled, defenderServiceEnabled);
            }, cancellationToken).ConfigureAwait(true);

            if (cancellationToken.IsCancellationRequested || this.XamlRoot == null)
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
        }
        catch (OperationCanceledException)
        {
            // Silent exit
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

    private void VirusThreatProtectionLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://threatsettings/");
    private void FirewallLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://network/");
    private void WindowsUpdateLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("ms-settings:windowsupdate");
    private void SmartScreenLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://smartscreenpua/");
    private void RealTimeProtectionLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://threatsettings/");
    private void UACLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("ms-settings:useraccounts");
    private void TamperProtectionLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://threatsettings/");
    private void ControlledFolderAccessLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://ransomwareprotection/");

    private void BitLockerLink_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo { FileName = "ms-settings:deviceencryption", UseShellExecute = true }); }
        catch
        {
            try { Process.Start(new ProcessStartInfo { FileName = "control.exe", Arguments = "/name Microsoft.BitLockerDriveEncryption", UseShellExecute = true }); }
            catch (Exception ex) { ErrorLogging.LogDebug(ex); }
        }
    }

    private void DefenderServiceLink_Click(object sender, RoutedEventArgs e) => OpenWindowsSecurityPage("windowsdefender://threatsettings/");

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