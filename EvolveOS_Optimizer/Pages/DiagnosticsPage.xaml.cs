// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License. 

using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.Settings;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class DiagnosticsPage : Page, IPurgeable
    {
        public DiagnosticsPageViewModel? ViewModel { get; } = DiagnosticsPageViewModel.Current;

        public static readonly DependencyProperty IsAiEnabledProperty =
                DependencyProperty.Register(nameof(IsAiEnabled), typeof(bool), typeof(DiagnosticsPage), new PropertyMetadata(false));

        public bool IsAiEnabled
        {
            get => (bool)GetValue(IsAiEnabledProperty);
            set => SetValue(IsAiEnabledProperty, value);
        }

        private int _navGeneration = 0;
        private bool _isCurrentPageActive = false;
        private bool _isInitialized = false;

        public static string RequestedPaneOnLoad = "";
        public static Action<string>? ExternalPaneRequest;

        public static Action? RequestDnsUIUpdate;

        private readonly Dictionary<IDNSCryptSetting, ComboBox> _controls;

        #region Fields (Maintenance)
        private bool _isShowingResult = false;
        private readonly Dictionary<string, StringBuilder> _scanResults = new()
        {
            { "DISM", new StringBuilder() },
            { "SFC", new StringBuilder() },
            { "CHKDSK", new StringBuilder() }
        };
        private Process? _runningProcess;
        private CancellationTokenSource? _cancellationTokenSource;
        private int _currentProcessId;
        public int selectedCount = 0;
        private string? _pendingScrollTarget;
        private int _sfcPrefaceLinesSkipped;
        private DateTime _lastProgressUpdateTime = DateTime.MinValue;
        #endregion

        #region Constructor
        public DiagnosticsPage()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            this.InitializeComponent();

            if (SettingsEngine.IsHighPerformanceModeEnabled)
            {
                this.NavigationCacheMode = NavigationCacheMode.Required;
            }
            else
            {
                this.NavigationCacheMode = NavigationCacheMode.Disabled;
            }

            RefreshAiStatus();

            LocalMachineSettingsEngine.SettingChanged += (s, e) =>
            {
                if (e.Contains("ApiKey") || e == "ActiveAiProvider")
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        RefreshAiStatus();
                    });
                }
            };

            this.DataContext = ViewModel;

            _controls = new Dictionary<IDNSCryptSetting, ComboBox>
            {
                {new DNSCryptSetting_ipv4_servers(), ipv4_servers},
                {new DNSCryptSetting_ipv6_servers(), ipv6_servers},
                {new DNSCryptSetting_dnscrypt_servers(), dnscrypt_servers},
                {new DNSCryptSetting_doh_servers(), doh_servers},
                {new DNSCryptSetting_require_dnssec(), require_dnssec},
                {new DNSCryptSetting_require_nolog(), require_nolog},
                {new DNSCryptSetting_require_nofilter(), require_nofilter},
                {new DNSCryptSetting_bootstrap_resolvers(), bootstrap_resolvers},
                {new DNSCryptSetting_dnscrypt_ephemeral_keys(), dnscrypt_ephemeral_keys},
                {new DNSCryptSetting_tls_disable_session_tickets(), tls_disable_session_tickets},
                {new DNSCryptSetting_netprobe_timeout(), netprobe_timeout},
                {new DNSCryptSetting_netprobe_address(), netprobe_address},
                {new DNSCryptSetting_block_ipv6(), block_ipv6},
                {new DNSCryptSetting_reject_ttl(), reject_ttl},
            };

            BtnDownloadInstall.RenderTransform = new TransformGroup();
            ((TransformGroup)BtnDownloadInstall.RenderTransform).Children.Add(new TranslateTransform());

            this.Loaded += DiagnosticsPage_Loaded;
            this.Unloaded += DiagnosticsPage_Unloaded;
        }
        #endregion

        #region View Model Property Changed (UI Refresher)
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (ViewModel == null || !_isCurrentPageActive) return;

            if (e.PropertyName == nameof(ViewModel.IsScanning) ||
                e.PropertyName == nameof(ViewModel.HardwareScannerVisibility))
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    this.Bindings.Update();

                    if (!ViewModel.IsScanning && ViewModel.DetectedHardwareIssues.Count > 0)
                    {
                        HeartbeatStoryboard?.Stop();

                        HeartbeatScanner.Visibility = Visibility.Collapsed;
                        HardwareIssuesListView.Visibility = Visibility.Visible;
                    }
                    else if (ViewModel.IsScanning)
                    {
                        HeartbeatScanner.Visibility = Visibility.Visible;
                        HardwareIssuesListView.Visibility = Visibility.Collapsed;
                        HeartbeatStoryboard?.Begin();
                    }

                    this.UpdateLayout();
                });
            }
        }
        #endregion

        #region Event Handlers
        private async void DiagnosticsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.Content == null)
            {
                Debug.WriteLine("[DiagnosticsPage] Singleton UI missing. Re-hydrating visual tree...");

                try
                {
                    Application.LoadComponent(this,
                        new Uri("ms-appx:///Pages/DiagnosticsPage.xaml"),
                        ComponentResourceLocation.Application);

                    this.DataContext = ViewModel;

                    _controls.Clear();
                    _controls.Add(new DNSCryptSetting_ipv4_servers(), ipv4_servers);
                    _controls.Add(new DNSCryptSetting_ipv6_servers(), ipv6_servers);
                    _controls.Add(new DNSCryptSetting_dnscrypt_servers(), dnscrypt_servers);
                    _controls.Add(new DNSCryptSetting_doh_servers(), doh_servers);
                    _controls.Add(new DNSCryptSetting_require_dnssec(), require_dnssec);
                    _controls.Add(new DNSCryptSetting_require_nolog(), require_nolog);
                    _controls.Add(new DNSCryptSetting_require_nofilter(), require_nofilter);
                    _controls.Add(new DNSCryptSetting_bootstrap_resolvers(), bootstrap_resolvers);
                    _controls.Add(new DNSCryptSetting_dnscrypt_ephemeral_keys(), dnscrypt_ephemeral_keys);
                    _controls.Add(new DNSCryptSetting_tls_disable_session_tickets(), tls_disable_session_tickets);
                    _controls.Add(new DNSCryptSetting_netprobe_timeout(), netprobe_timeout);
                    _controls.Add(new DNSCryptSetting_netprobe_address(), netprobe_address);
                    _controls.Add(new DNSCryptSetting_block_ipv6(), block_ipv6);
                    _controls.Add(new DNSCryptSetting_reject_ttl(), reject_ttl);

                    BtnDownloadInstall.RenderTransform = new TransformGroup();
                    ((TransformGroup)BtnDownloadInstall.RenderTransform).Children.Add(new TranslateTransform());

                    ViewModel?.RebuildVisualPoints();

                    _isInitialized = false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DiagnosticsPage] Re-hydration failure: {ex.Message}");
                }
            }

            _isCurrentPageActive = true;
            int currentGen = ++_navGeneration;

            ExternalPaneRequest = SwitchToPane;
            RequestDnsUIUpdate = UpdateDnsCryptControls;

            if (!string.IsNullOrEmpty(RequestedPaneOnLoad))
            {
                SwitchToPane(RequestedPaneOnLoad);
                RequestedPaneOnLoad = "";
            }

            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                ViewModel.ShowSecurityIssuesRequested -= ViewModel_ShowSecurityIssuesRequested;
                ViewModel.CloseActiveDialogsRequested -= ViewModel_CloseActiveDialogsRequested;
                ViewModel.OnAddProcessToExclusionListCommandCompleted -= OnAddProcessToExclusionListCommandCompleted;
                ViewModel.OnRemoveProcessFromExclusionListCommandCompleted -= OnRemoveProcessFromExclusionListCommandCompletedCallback;
                ViewModel.OnOptimizeCommandCompleted -= OnOptimizeCommandCompleted;
                ViewModel.OpenDnsToolkitRequested -= ViewModel_OpenDnsToolkitRequested;

                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                ViewModel.ShowSecurityIssuesRequested += ViewModel_ShowSecurityIssuesRequested;
                ViewModel.CloseActiveDialogsRequested += ViewModel_CloseActiveDialogsRequested;
                ViewModel.OnAddProcessToExclusionListCommandCompleted += OnAddProcessToExclusionListCommandCompleted;
                ViewModel.OnRemoveProcessFromExclusionListCommandCompleted += OnRemoveProcessFromExclusionListCommandCompletedCallback;
                ViewModel.OnOptimizeCommandCompleted += OnOptimizeCommandCompleted;
                ViewModel.OpenDnsToolkitRequested += ViewModel_OpenDnsToolkitRequested;

                if (ViewModel.HardwareScannerVisibility == Visibility.Visible) HeartbeatStoryboard?.Begin();
                if (ViewModel.ScanningVisibility == Visibility.Visible) SystemSonarStoryboard?.Begin();
                if (ViewModel.EventEmptyStateVisibility == Visibility.Visible) RadarSpinStoryboard?.Begin();
            }

            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (currentGen != _navGeneration || !_isCurrentPageActive) return;
                ViewModel?.ResumeUiUpdates();
            });

            if (!_isInitialized)
            {
                if (ViewModel?.SelectedProcess == null && ViewModel?.Processes.Count > 0)
                {
                    ViewModel.SelectedProcess = ViewModel.Processes[0];
                }

                await CalculateSystemHealthAsync();
                UpdateDnsCryptControls();
                AnimateInstallButton();
                ValidateButtonStates();

                AiExplainerService.PreWarmConnection();

                _isInitialized = true;
                Debug.WriteLine("[DiagnosticsPage] Initial/Re-hydrated data loaded.");
            }

            if (!string.IsNullOrEmpty(_pendingScrollTarget))
            {
                await ScrollToElementHelper.ScrollToElementAsync(this, _pendingScrollTarget);
                _pendingScrollTarget = null;
            }

            try
            {
                bool wasInstalled = HardwareDriverHelper.IsPawnIoInstalled();

                bool isInstalledNow = await HardwareDriverHelper.EnsurePawnIoInstalledAsync(this.XamlRoot);

                if (!wasInstalled && isInstalledNow)
                {
                    ViewModel?.PauseUiUpdates();
                    HardwareTemperatureService.Instance.Close();
                    HardwareTemperatureService.Instance.Initialize();
                    ViewModel?.ResumeUiUpdates();
                }

                UpdateDriverButtonStates();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DiagnosticsPage] PawnIO Initialization Error: {ex.Message}");
            }
        }

        private void DiagnosticsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                RadarSpinStoryboard?.Stop();

                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                ViewModel.ShowSecurityIssuesRequested -= ViewModel_ShowSecurityIssuesRequested;
                ViewModel.CloseActiveDialogsRequested -= ViewModel_CloseActiveDialogsRequested;
                ViewModel.OnAddProcessToExclusionListCommandCompleted -= OnAddProcessToExclusionListCommandCompleted;
                ViewModel.OnRemoveProcessFromExclusionListCommandCompleted -= OnRemoveProcessFromExclusionListCommandCompletedCallback;
                ViewModel.OnOptimizeCommandCompleted -= OnOptimizeCommandCompleted;
                ViewModel.OpenDnsToolkitRequested -= ViewModel_OpenDnsToolkitRequested;
            }

            ExternalPaneRequest = null;

            _ = Purge();
        }

        private void SwitchToPane(string paneName)
        {
            if (paneName == "Security" && SecurityToggle != null)
            {
                SecurityToggle.IsChecked = true;
                SecurityToggle_Click(this, new RoutedEventArgs());
            }
            else if (paneName == "Maintenance" && MaintenanceToggle != null)
            {
                MaintenanceToggle.IsChecked = true;
                MaintenanceToggle_Click(this, new RoutedEventArgs());
            }
            else if (paneName == "DnsCrypt" && this.FindName("DnsCryptToggle") is ToggleButton dnsToggle)
            {
                dnsToggle.IsChecked = true;
                DnsCryptToggle_Click(this, new RoutedEventArgs());
            }
        }

        private void ViewModel_ShowSecurityIssuesRequested(List<string> issues)
        {
            DispatcherQueue?.TryEnqueue(async () =>
            {
                try
                {
                    var stackPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };

                    foreach (var issue in issues)
                    {
                        stackPanel.Children.Add(new TextBlock
                        {
                            Text = $"• {issue}",
                            TextWrapping = TextWrapping.Wrap,
                            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("ms-appx:///Assets/Fonts/Jura-Regular.ttf#Jura")
                        });
                    }

                    var dialog = new ContentDialog
                    {
                        Title = ResourceString.GetString("SecurityPage_WarningsTitle") ?? "Security Warnings Found",
                        Content = new ScrollViewer { Content = stackPanel, MaxHeight = 300, Padding = new Thickness(0, 0, 16, 0) },
                        CloseButtonText = ResourceString.GetString("Dialog_Close") ?? "Close",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };

                    if (Application.Current.Resources.TryGetValue("DefaultContentDialogStyle", out object style))
                    {
                        dialog.Style = (Style)style;
                    }

                    await dialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DiagnosticsPage] Security Dialog Error: {ex.Message}");
                }
            });
        }

        private void HeartbeatScanner_Loaded(object sender, RoutedEventArgs e)
        {
            HeartbeatStoryboard?.Begin();
        }

        private void HeartbeatScanner_Unloaded(object sender, RoutedEventArgs e)
        {
            HeartbeatStoryboard?.Stop();
        }

        private void SystemSonar_Loaded(object sender, RoutedEventArgs e)
        {
            SystemSonarStoryboard?.Begin();
            RadarSpinStoryboard?.Begin();
        }

        private void SystemSonar_Unloaded(object sender, RoutedEventArgs e)
        {
            SystemSonarStoryboard?.Stop();
            RadarSpinStoryboard?.Stop();
        }

        private async void FixHardwareButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = ViewModel;
            if (vm != null && sender is Button button && button.DataContext is HardwareIssue selectedIssue)
            {
                await vm.FixHardwareAsync(selectedIssue);
            }
        }

        private async void StartFullScan_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null && !ViewModel.IsScanning)
            {
                await ViewModel.ExecuteFullScanAsync();
                await CalculateSystemHealthAsync();
            }
        }

        private async void Expander_Expanding(Expander sender, ExpanderExpandingEventArgs args)
        {
            await Task.Delay(150);

            sender.StartBringIntoView(new BringIntoViewOptions
            {
                AnimationDesired = true
            });
        }

        private void HudToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (HudViewbox.Visibility == Visibility.Visible)
            {
                HudViewbox.Visibility = Visibility.Collapsed;
                HudBackgroundBorder.Visibility = Visibility.Collapsed;
                HudCloseBtn.Visibility = Visibility.Collapsed;

                HudOpenBtn.Visibility = Visibility.Visible;
            }
            else
            {
                HudViewbox.Visibility = Visibility.Visible;
                HudBackgroundBorder.Visibility = Visibility.Visible;
                HudCloseBtn.Visibility = Visibility.Visible;

                HudOpenBtn.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Callbacks (Maintenance)
        private void OnRemoveProcessFromExclusionListCommandCompletedCallback()
        {
            if (this.FindName("ProcessExclusionList") is Control ctrl) SetFocusTo(ctrl);
        }
        #endregion

        #region Navigation & Lifecycle
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            this.Bindings.Initialize();
            this.Bindings.Update();

            _isCurrentPageActive = true;

            ViewModel?.ResumeUiUpdates();

            if (e.Parameter is string optionTag && !string.IsNullOrEmpty(optionTag))
            {
                _pendingScrollTarget = optionTag;
            }

            await CalculateSystemHealthAsync();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _isCurrentPageActive = false;

            ViewModel?.PauseUiUpdates();

            SystemSonarStoryboard?.Stop();
            HeartbeatStoryboard?.Stop();
            RadarSpinStoryboard?.Stop();

            this.Bindings.StopTracking();

            base.OnNavigatedFrom(e);
        }
        #endregion

        #region Command Line & System Tools Processing
        private async Task RunCommandsAsync(bool isRepair)
        {
            var pendingCommands = new List<(string Name, string Args, string Schedule)>();

            if (DismCheckBox?.IsChecked == true)
                pendingCommands.Add(("DISM", isRepair ? "/Online /Cleanup-Image /RestoreHealth" : "/Online /Cleanup-Image /ScanHealth", string.Empty));

            if (SfcCheckBox?.IsChecked == true)
                pendingCommands.Add(("SFC", isRepair ? "/scannow" : "/verifyonly", string.Empty));

            if (ChkdskCheckBox?.IsChecked == true)
                pendingCommands.Add(("CHKDSK", isRepair ? "/f" : "", "echo Y|chkdsk {DriveRoot} /f"));

            if (pendingCommands.Count == 0) return;

            DispatcherQueue?.TryEnqueue(() =>
            {
                if (ScanRepairPanel != null) ScanRepairPanel.Visibility = Visibility.Collapsed;
                if (StopButton != null)
                {
                    StopButton.Visibility = Visibility.Visible;
                    StopButton.IsEnabled = true;
                }
                if (RepairProgressBar != null) RepairProgressBar.Value = 0;
            });

            _currentProcessId = 0;

            try { _cancellationTokenSource?.Cancel(); _cancellationTokenSource?.Dispose(); } catch { }
            _cancellationTokenSource = new CancellationTokenSource();
            var ct = _cancellationTokenSource.Token;

            var current = 0;
            var selectedNames = new List<string>();
            var wasCancelled = false;
            var hasError = false;

            _lastProgressUpdateTime = DateTime.UtcNow;

            try
            {
                foreach (var (name, args, scheduleTemplate) in pendingCommands)
                {
                    try
                    {
                        if (ct.IsCancellationRequested)
                        {
                            wasCancelled = true;
                            break;
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        wasCancelled = true;
                        break;
                    }

                    current++;
                    selectedNames.Add(name);

                    string formatString = isRepair
                        ? ResourceString.GetString("RepairInProgress") ?? "Repairing {2}..."
                        : ResourceString.GetString("ScanInProgress") ?? "Scanning {2}...";

                    DispatcherQueue?.TryEnqueue(() =>
                    {
                        if (RepairStatusText != null) RepairStatusText.Text = string.Format(formatString, current, selectedCount, name);
                        if (RepairProgressBar != null) RepairProgressBar.Value = 0;
                    });

                    if (name == "CHKDSK" && isRepair)
                    {
                        var driveRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))?.TrimEnd('\\') ?? "C:";

                        DispatcherQueue?.TryEnqueue(() =>
                        {
                            App.ShowNotification(ResourceString.GetString("Repair") ?? "Repair", ResourceString.GetString("ScheduledLater") ?? "Scheduled on restart", InfoBarSeverity.Success, 5000);
                            if (ChkdskCheckBox != null) ChkdskCheckBox.IsEnabled = false;
                        });

                        _scanResults[name].Clear();
                        _scanResults[name].AppendLine(ResourceString.GetString("ScheduledLater"));

                        if (!string.IsNullOrEmpty(scheduleTemplate))
                        {
                            var scheduleCmd = scheduleTemplate.Replace("{DriveRoot}", driveRoot);
                            await CommandExecutor.StartInCmd(scheduleCmd);
                        }
                        continue;
                    }

                    try
                    {
                        await RunCommandAsync(name, args, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        wasCancelled = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        await ErrorLogging.LogInfo($"Error running {name}: {ex.Message}");
                        _scanResults[name].AppendLine($"Error: {ex.Message}");
                        hasError = true;

                        try
                        {
                            if (ct.IsCancellationRequested)
                            {
                                wasCancelled = true;
                                break;
                            }
                        }
                        catch (ObjectDisposedException) { wasCancelled = true; break; }
                    }
                }
            }
            finally
            {
                _currentProcessId = 0;
                ResetUIState();

                DispatcherQueue?.TryEnqueue(async () =>
                {
                    if (!_isCurrentPageActive) return;

                    if (wasCancelled)
                    {
                        App.ShowNotification(ResourceString.GetString("Repair") ?? "Repair", ResourceString.GetString("OperationStopped") ?? "Stopped", InfoBarSeverity.Error, 5000);
                    }
                    else if (hasError)
                    {
                        App.ShowNotification(ResourceString.GetString("Repair") ?? "Repair", ResourceString.GetString("UnexpectedError") ?? "Error", InfoBarSeverity.Error, 5000);
                        if (selectedNames.Count > 0) await ShowScanResultsDialogAsync(selectedNames);
                    }
                    else
                    {
                        App.ShowNotification(ResourceString.GetString("Repair") ?? "Repair", ResourceString.GetString("OperationCompleted") ?? "Completed", InfoBarSeverity.Success, 5000);
                        if (selectedNames.Count > 0) await ShowScanResultsDialogAsync(selectedNames);
                    }
                });

                try
                {
                    _cancellationTokenSource?.Dispose();
                    _cancellationTokenSource = null;
                }
                catch { /* Ignore double-dispose */ }
            }
        }

        private async Task RunCommandAsync(string name, string args, CancellationToken ct)
        {
            _scanResults[name].Clear();

            if (name == "SFC")
            {
                _sfcPrefaceLinesSkipped = 0;
            }

            var toolExecutable = name switch
            {
                "DISM" => "dism.exe",
                "SFC" => "sfc.exe",
                "CHKDSK" => "chkdsk.exe",
                _ => name + ".exe"
            };

            var fileName = GetSystemToolPath(toolExecutable);

            try
            {
                await ConPtyProcessRunner.RunAsync(
                    $"\"{fileName}\" {args}",
                    line =>
                    {
                        ErrorLogging.LogDebug($"Output: {line}");
                        HandleOutputLine(name, line);
                    },
                    ct,
                    processId => _currentProcessId = processId);
            }
            catch (OperationCanceledException)
            {
                await ErrorLogging.LogInfo($"Operation cancelled for {name}");
                throw;
            }
            catch (Exception ex)
            {
                await ErrorLogging.LogInfo($"ConPTY failed for {name}, falling back to standard: {ex.Message}");
                await RunCommandStandardAsync(name, fileName, args, ct);
            }
        }

        private async Task RunCommandStandardAsync(string name, string fileName, string args, CancellationToken ct)
        {
            var outputEncoding = name.Equals("SFC", StringComparison.OrdinalIgnoreCase)
                ? Encoding.Unicode
                : Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = outputEncoding,
                StandardErrorEncoding = outputEncoding
            };

            _runningProcess = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true };

            try
            {
                _runningProcess.Start();
                _currentProcessId = _runningProcess.Id;

                var outputTask = ReadStreamAsync(_runningProcess.StandardOutput, name, isError: false, ct);
                var errorTask = ReadStreamAsync(_runningProcess.StandardError, name, isError: true, ct);

                try
                {
                    await Task.WhenAll(_runningProcess.WaitForExitAsync(ct), outputTask, errorTask);
                }
                catch (OperationCanceledException)
                {
                    await ProcessTerminator.KillProcessTreeAsync(_runningProcess.Id);
                    throw;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await ErrorLogging.LogInfo($"Failed to start {name}: {ex.Message}");
                _scanResults[name].AppendLine(ex.Message);
                throw;
            }
            finally
            {
                _runningProcess?.Dispose();
                _runningProcess = null;
                _currentProcessId = 0;
            }
        }

        private async Task ReadStreamAsync(StreamReader reader, string name, bool isError, CancellationToken ct)
        {
            var buffer = new char[256];
            var lineBuilder = new StringBuilder();

            while (!ct.IsCancellationRequested)
            {
                int read;
                try
                {
                    read = await reader.ReadAsync(buffer, 0, buffer.Length);
                }
                catch
                {
                    break;
                }

                if (read == 0)
                {
                    FlushLine(lineBuilder, name, isError);
                    break;
                }

                for (var i = 0; i < read; i++)
                {
                    var ch = buffer[i];
                    if (ch == '\r' || ch == '\n')
                    {
                        FlushLine(lineBuilder, name, isError);
                    }
                    else
                    {
                        lineBuilder.Append(ch);
                    }
                }
            }
        }

        private void FlushLine(StringBuilder lineBuilder, string name, bool isError)
        {
            if (lineBuilder.Length == 0)
            {
                return;
            }

            var line = lineBuilder.ToString();
            lineBuilder.Clear();

            if (isError)
            {
                ErrorLogging.LogDebug($"Error: {line}");
                _scanResults[name].AppendLine(line);
                return;
            }

            HandleOutputLine(name, line);
        }

        private void HandleOutputLine(string name, string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            UpdateProgress(name, line);

            if (name == "DISM")
            {
                line = Regex.Replace(line, @"\[\s*[= ]*\s*\d+(?:[\.,]\d+)?%\s*[= ]*\]\s*", string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(line)) return;
            }

            var isProgress = name switch
            {
                "DISM" => Regex.IsMatch(line, @"^\s*\[\s*[= ]*\s*(\d+(\.\d+)?)%\s*[= ]*\]\s*$"),
                "SFC" => Regex.IsMatch(line, @"^\s*[^\d\r\n]*?(\d{1,3}(?:[\.,]\d+)?)\s*%\s*[^\d\r\n]*$"),
                "CHKDSK" => Regex.IsMatch(line, @"^\s*[^\d\r\n]*?(\d{1,3}(?:[\.,]\d+)?)\s*%\s*[^\d\r\n]*$"),
                _ => false
            };

            if (isProgress) return;

            if (name == "SFC" && _sfcPrefaceLinesSkipped < 2)
            {
                _sfcPrefaceLinesSkipped++;
                return;
            }

            _scanResults[name].AppendLine(line);
        }

        private void UpdateProgress(string commandName, string data)
        {
            if (string.IsNullOrEmpty(data) || !_isCurrentPageActive) return;

            var percentage = 0;

            try
            {
                if (commandName == "DISM")
                {
                    var match = Regex.Match(data, @"\[\s*[= ]*\s*(\d+(\.\d+)?)%\s*[= ]*\]");
                    if (match.Success) percentage = (int)Math.Round(double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
                }
                else if (commandName == "SFC")
                {
                    var match = Regex.Match(data, @"(\d+)%", RegexOptions.IgnoreCase);
                    if (match.Success) percentage = int.Parse(match.Groups[1].Value);
                }
                else if (commandName == "CHKDSK")
                {
                    var match = Regex.Match(data, @"(\d+(?:[\.,]\d+)?)\s*%", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var percentageText = match.Groups[1].Value.Replace(',', '.');
                        percentage = (int)Math.Round(double.Parse(percentageText, CultureInfo.InvariantCulture));
                    }
                }

                if (percentage > 0 && percentage <= 100)
                {
                    var now = DateTime.UtcNow;

                    if ((now - _lastProgressUpdateTime).TotalMilliseconds > 200 || percentage == 100)
                    {
                        _lastProgressUpdateTime = now;

                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (!_isCurrentPageActive) return;

                            if (RepairProgressBar != null) RepairProgressBar.Value = percentage;
                            if (RepairPercentageText != null) RepairPercentageText.Text = $"{percentage}%";
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"Error updating progress: {ex.Message}");
            }
        }

        private static string GetSystemToolPath(string toolExecutable)
        {
            var winDir = Environment.GetEnvironmentVariable("windir");
            if (string.IsNullOrEmpty(winDir)) return toolExecutable;

            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            {
                var sysNativePath = Path.Combine(winDir, "SysNative", toolExecutable);
                if (File.Exists(sysNativePath)) return sysNativePath;
            }

            var system32Path = Path.Combine(winDir, "System32", toolExecutable);
            if (File.Exists(system32Path)) return system32Path;

            return Path.Combine(winDir, toolExecutable);
        }

        private async Task StopCurrentOperationAsync()
        {
            _cancellationTokenSource?.Cancel();

            var processId = _currentProcessId;
            if (processId > 0)
            {
                await ProcessTerminator.KillProcessTreeAsync(processId);
            }

            if (_runningProcess != null)
            {
                try
                {
                    if (!_runningProcess.HasExited)
                    {
                        await ProcessTerminator.KillProcessTreeAsync(_runningProcess.Id);
                    }
                }
                catch { }
            }
        }
        #endregion

        #region Event Handlers
        private async void OnScanButtonClick(object sender, RoutedEventArgs e)
        {
            if (selectedCount == 0) { return; }
            await RunCommandsAsync(isRepair: false);
        }

        private async void OnRepairButtonClick(object sender, RoutedEventArgs e)
        {
            if (selectedCount == 0) { return; }
            await RunCommandsAsync(isRepair: true);
        }

        private async void CleanNowButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            if (ViewModel.CanOptimize)
            {
                await ViewModel.Optimize(Core.Enums.Memory.Optimization.Reason.Manual);
            }
            else
            {
                App.ShowNotification("System Cleanup", "Please select at least one cleanup area first.", InfoBarSeverity.Warning, 3000);
            }
        }

        private async void OnStopButtonClick(object sender, RoutedEventArgs e)
        {
            if (StopButton != null) StopButton.IsEnabled = false;
            await StopCurrentOperationAsync();
        }

        private async void BatteryHealthButton_Click(object sender, RoutedEventArgs e)
        {
            var reportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "batteryreport.html");
            var command = $"%SystemRoot%\\System32\\powercfg.exe /batteryreport /output \"{reportPath}\"";

            var result = await CommandExecutor.StartInCmd(command);

            if (result == 0 && File.Exists(reportPath))
            {
                App.ShowNotification(ResourceString.GetString("BatteryStatus") ?? "Battery", ResourceString.GetString("ReportSaved") ?? "Report Saved", InfoBarSeverity.Success, 5000);
                return;
            }
            App.ShowNotification(ResourceString.GetString("BatteryStatus") ?? "Battery", ResourceString.GetString("UnexpectedError") ?? "Error", InfoBarSeverity.Error, 5000);
        }

        private async void MemoryHealthButton_Click(object sender, RoutedEventArgs e)
        {
            var memDialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
                PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                SecondaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                Title = ResourceString.GetString("MemoryDiagnosticDialogTitle") ?? "Memory Diagnostic",
                Content = ResourceString.GetString("MemoryDiagnosticDialogText") ?? "Restart required.",
                PrimaryButtonText = ResourceString.GetString("RestartNow") ?? "Restart Now",
                SecondaryButtonText = ResourceString.GetString("ScheduleLater") ?? "Later",
                CloseButtonText = ResourceString.GetString("Cancel") ?? "Cancel"
            };

            memDialog.PrimaryButtonClick += async (sender, args) =>
            {
                await CommandExecutor.StartInCmd("bcdedit /bootsequence {memdiag} && shutdown /r /t 0");
            };

            memDialog.SecondaryButtonClick += async (sender, args) =>
            {
                App.ShowNotification(ResourceString.GetString("MemoryDiagnosticDialogTitle") ?? "Memory", ResourceString.GetString("ScheduledLater") ?? "Scheduled", InfoBarSeverity.Success, 5000);
                if (this.FindName("MemCheckButton") is Button btn) btn.IsEnabled = false;
                await CommandExecutor.StartInCmd("bcdedit /bootsequence {memdiag}");
            };

            await memDialog.ShowAsync();
        }

        private async void EventViewerSettingsCard_Click(object sender, RoutedEventArgs e)
        {
            await CommandExecutor.StartInCmd("eventvwr.msc");
        }

        private async void DiskOptimizationsButton_Click(object sender, RoutedEventArgs e)
        {
            await CommandExecutor.StartInCmd("%SystemRoot%\\System32\\dfrgui.exe");
        }

        private void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ValidateButtonStates();
        }

        private void ValidateButtonStates()
        {
            if (ScanButton == null || RepairButton == null) return;

            selectedCount = 0;
            if (DismCheckBox?.IsChecked == true) selectedCount++;
            if (SfcCheckBox?.IsChecked == true) selectedCount++;
            if (ChkdskCheckBox?.IsChecked == true) selectedCount++;

            bool isAnyChecked = selectedCount > 0;

            ScanButton.IsEnabled = isAnyChecked;
            RepairButton.IsEnabled = isAnyChecked;
        }

        private void OnSliderPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Slider slider) slider.Focus(FocusState.Pointer);
        }

        private async void OpenPortScanner_Click(object sender, RoutedEventArgs e)
        {
            PortScannerDialog.XamlRoot = this.XamlRoot;

            if (ViewModel != null && ViewModel.OpenPorts.Count == 0 && !ViewModel.IsPortScanRunning)
            {
                ViewModel.ScanNetworkPortsCommand.Execute(null);
            }

            await PortScannerDialog.ShowAsync();
        }

        private void ClosePortScanner_Click(object sender, RoutedEventArgs e)
        {
            PortScannerDialog.Hide();
        }

        private async void DnsToolkitButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                DnsToolkitDialog.XamlRoot = this.XamlRoot;

                _ = ViewModel.UpdateSystemDnsDisplayAsync();
                await DnsToolkitDialog.ShowAsync();
            }
        }

        private void CloseDnsToolkit_Click(object sender, RoutedEventArgs e)
        {
            DnsToolkitDialog.Hide();
        }

        private async void ViewModel_OpenDnsToolkitRequested()
        {
            DnsToolkitButton_Click(this, new RoutedEventArgs());
        }

        private void ApplyBenchmarkPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Microsoft.UI.Xaml.Controls.Button btn && btn.DataContext is DnsPreset preset)
            {
                ViewModel?.ApplyDnsPresetCommand.Execute(preset);
            }
        }

        private async void InstallAdvancedSensorsButton_Click(object sender, RoutedEventArgs e)
        {
            bool installed = await HardwareDriverHelper.EnsurePawnIoInstalledAsync(this.XamlRoot, forcePrompt: true);

            if (installed)
            {
                HardwareTemperatureService.Instance.Close();
                HardwareTemperatureService.Instance.Initialize();

                DiagnosticsPageViewModel.Current.SendSystemNotification(1,
                    "Sensors Active",
                    "PawnIO installed successfully. Advanced thermal monitoring is now online.");
            }

            UpdateDriverButtonStates();
        }

        private async void UninstallAdvancedSensorsButton_Click(object sender, RoutedEventArgs e)
        {
            HardwareTemperatureService.Instance.Close();

            bool uninstalled = await HardwareDriverHelper.UninstallPawnIoAsync();

            if (uninstalled)
            {
                HardwareTemperatureService.Instance.Initialize();

                DiagnosticsPageViewModel.Current.SendSystemNotification(4,
                    "Sensors Removed",
                    "PawnIO was uninstalled successfully. Advanced thermal monitoring is now disabled.");
            }
            else
            {
                HardwareTemperatureService.Instance.Initialize();

                DiagnosticsPageViewModel.Current.SendSystemNotification(3,
                    "Uninstall Failed",
                    "Could not completely remove PawnIO. The driver binary may currently be locked by another process.");
            }

            UpdateDriverButtonStates();
        }
        #endregion

        #region AI Event Log Explainer
        public void RefreshAiStatus()
        {
            var activeProvider = LocalMachineSettingsEngine.ActiveAiProvider;
            bool hasKey = activeProvider switch
            {
                AiProvider.Groq => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.GroqApiKey),
                AiProvider.Gemini => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.GeminiApiKey),
                AiProvider.OpenRouter => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.OpenRouterApiKey),
                AiProvider.Cohere => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.CohereApiKey),
                AiProvider.Mistral => !string.IsNullOrWhiteSpace(LocalMachineSettingsEngine.MistralApiKey),
                _ => false
            };

            // System.Diagnostics.Debug.WriteLine($"AI Status Update: Provider {activeProvider}, HasKey: {hasKey}");

            IsAiEnabled = hasKey;
        }

        private async void ExplainEvent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SystemEventItem vm)
            {
                var flyout = button.Flyout as Flyout;
                if (flyout == null) return;

                var stackPanel = flyout.Content as StackPanel;
                var textBlock = stackPanel?.Children.OfType<TextBlock>().FirstOrDefault(x => x.Tag?.ToString() == "AiExplanationText");

                if (textBlock == null) return;

                textBlock.Text = ResourceString.GetString("ai_explainer_thinking") ?? "Thinking...";

                string context = $"Source: {vm.SourceName}\n" +
                                 $"Event ID: {vm.EventId}\n" +
                                 $"Time: {vm.FormattedTime}\n" +
                                 $"Severity: {vm.StatusGlyph} (Visual Indicator)\n" +
                                 $"Message: {vm.Message}\n" +
                                 $"Detailed Payload: {vm.FullMessage}";

                string category = ResourceString.GetString("diag_page_event_category_name") ?? "Windows System Event";

                string explanation = await AiExplainerService.ExplainGenericItemAsync(
                    itemName: $"Event {vm.EventId} ({vm.SourceName})",
                    itemCategory: category,
                    contextDetails: context
                );

                textBlock.Text = explanation;
            }
        }

        private async void ExplainMemoryArea_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string memoryAreaName)
            {
                var flyout = button.Flyout as Flyout;
                if (flyout == null) return;

                var stackPanel = flyout.Content as StackPanel;

                var textBlock = stackPanel?.Children.OfType<TextBlock>().FirstOrDefault(x => x.Tag?.ToString() == "AiExplanationText");

                if (textBlock == null) return;

                textBlock.Text = ResourceString.GetString("ai_explainer_thinking") ?? "Thinking...";

                string context = $"Target: Windows Memory Management Area\nItem: {memoryAreaName}";
                string category = ResourceString.GetString("diag_category_memory") ?? "Windows Memory Architecture";

                string explanation = await AiExplainerService.ExplainGenericItemAsync(
                    itemName: memoryAreaName,
                    itemCategory: category,
                    contextDetails: context
                );

                textBlock.Text = explanation;
            }
        }

        private async void ExplainRepairTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string repairTaskName)
            {
                var flyout = button.Flyout as Flyout;
                if (flyout == null) return;

                var stackPanel = flyout.Content as StackPanel;

                var textBlock = stackPanel?.Children.OfType<TextBlock>().FirstOrDefault(x => x.Tag?.ToString() == "AiExplanationText");

                if (textBlock == null) return;

                textBlock.Text = ResourceString.GetString("ai_explainer_thinking") ?? "Thinking...";

                string context = $"Target: Windows System Repair Utility\nUtility Name: {repairTaskName}";
                string category = ResourceString.GetString("diag_category_repair") ?? "Windows Command Line Utility";

                string explanation = await AiExplainerService.ExplainGenericItemAsync(
                    itemName: repairTaskName,
                    itemCategory: category,
                    contextDetails: context
                );

                textBlock.Text = explanation;
            }
        }

        private async void ExplainDnsOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string optionScope)
            {
                var flyout = button.Flyout as Flyout;
                if (flyout == null) return;

                var stackPanel = flyout.Content as StackPanel;

                var textBlock = stackPanel?.Children.OfType<TextBlock>().FirstOrDefault(x => x.Tag?.ToString() == "AiExplanationText");

                if (textBlock == null) return;

                textBlock.Text = ResourceString.GetString("ai_explainer_thinking") ?? "Thinking...";

                string context = $"Context: Windows DNS Configuration\nSettings Group: {optionScope}";
                string category = ResourceString.GetString("diag_category_dns") ?? "DNS & Networking Strategy";

                string explanation = await AiExplainerService.ExplainGenericItemAsync(
                    itemName: optionScope,
                    itemCategory: category,
                    contextDetails: context
                );

                textBlock.Text = explanation;
            }
        }

        private async void ExplainHardwareIssue_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is HardwareIssue issue)
            {
                var flyout = button.Flyout as Flyout;
                if (flyout == null) return;

                var stackPanel = flyout.Content as StackPanel;
                var textBlock = stackPanel?.Children.OfType<TextBlock>().FirstOrDefault(x => x.Tag?.ToString() == "AiExplanationText");

                if (textBlock == null) return;

                textBlock.Text = ResourceString.GetString("ai_explainer_thinking") ?? "Thinking...";

                string context = $"Device: {issue.ComponentDisplayName ?? "Unknown Device"}\n" +
                                 $"Hardware Type: {issue.HardwareType ?? "Unknown Type"}\n" +
                                 $"Error Code: {issue.ErrorCodeHex} - {issue.ErrorCodeDescription}\n" +
                                 $"Diagnostic Summary: {issue.IssueSummary}";

                string category = ResourceString.GetString("diag_category_hardware") ?? "Hardware & Driver Diagnostics";

                string explanation = await AiExplainerService.ExplainGenericItemAsync(
                    itemName: issue.ComponentDisplayName ?? "Unknown Device",
                    itemCategory: category,
                    contextDetails: context
                );

                textBlock.Text = explanation;
            }
        }
        #endregion

        #region Neural AI Explanations (Event Card Interaction)

        private void EventCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (ViewModel == null) return;

            if (sender is FrameworkElement card && card.DataContext is SystemEventItem eventItem)
            {
                ViewModel.AiSummary = NeuralAnalysisEngine.GenerateEventAnalysis(eventItem.EventId, eventItem.SourceName);
            }
        }

        private void HistoryCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (ViewModel == null) return;

            if (sender is FrameworkElement card && card.DataContext is DismissedEventCard historyItem)
            {
                if (int.TryParse(historyItem.EventId, out int parsedEventId))
                {
                    ViewModel.AiSummary = NeuralAnalysisEngine.GenerateEventAnalysis(parsedEventId, historyItem.SourceName ?? "Unknown");
                }
            }
        }

        #endregion

        #region View Model Callbacks
        private void OnAddProcessToExclusionListCommandCompleted()
        {
            Debug.WriteLine("Process added to exclusion list.");
        }

        private void ViewModel_CloseActiveDialogsRequested()
        {
            PortScannerDialog?.Hide();
        }

        private void OnOptimizeCommandCompleted(Memory.Optimization.Reason reason, string message)
        {
            if (_isShowingResult) return;
            _isShowingResult = true;

            if (LocalMachineSettingsEngine.DisableAllOptimizationResults)
            {
                _isShowingResult = false;

                if (_isCurrentPageActive)
                {
                    _ = CalculateSystemHealthAsync();
                }
                return;
            }

            this.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    if (!_isCurrentPageActive) return;

                    if (LocalMachineSettingsEngine.RestartExplorerAfterOptimization)
                    {
                        await Task.Delay(2000);
                        if (!_isCurrentPageActive) return;
                    }

                    await CalculateSystemHealthAsync();

                    #region For Testing
                    /*if (!LocalMachineSettingsEngine.ShowOptimizationNotifications)
                    {
                        var root = this.XamlRoot ?? MainWindow.Instance?.Content?.XamlRoot;

                        if (root != null)
                        {
                            var dialog = new ContentDialog
                            {
                                XamlRoot = root,
                                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                                BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
                                Title = ResourceString.GetString("title_optimization_result") ?? "Result",
                                Content = message,
                                CloseButtonText = ResourceString.GetString("btn_ok") ?? "OK",
                                DefaultButton = ContentDialogButton.Close
                            };
                            await dialog.ShowAsync();
                        }
                    }
                    else
                    {
                        NotificationManager.Show(message).Perform();
                    }*/
                    #endregion
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error showing optimization result: {ex.Message}");
                }
                finally
                {
                    _isShowingResult = false;
                }
            });
        }

        private void DnsCryptToggle_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            bool isOpen = DnsCryptToggle.IsChecked ?? false;

            if (isOpen)
            {
                if (SecurityToggle.IsChecked == true)
                {
                    SecurityToggle.IsChecked = false;
                    if (SecuritySplitView != null) SecuritySplitView.IsPaneOpen = false;
                }

                if (MaintenanceToggle.IsChecked == true)
                {
                    MaintenanceToggle.IsChecked = false;
                    if (MainSplitView != null) MainSplitView.IsPaneOpen = false;
                }

                if (DnsCryptSplitView != null) DnsCryptSplitView.IsPaneOpen = true;

                UpdateDnsCryptControls();
            }
            else
            {
                if (DnsCryptSplitView != null) DnsCryptSplitView.IsPaneOpen = false;
            }
        }

        private void MaintenanceToggle_Click(object sender, RoutedEventArgs e)
        {
            bool isOpen = MaintenanceToggle.IsChecked ?? false;

            if (isOpen)
            {
                if (SecurityToggle.IsChecked == true)
                {
                    SecurityToggle.IsChecked = false;
                    if (SecuritySplitView != null) SecuritySplitView.IsPaneOpen = false;
                }

                if (DnsCryptToggle != null && DnsCryptToggle.IsChecked == true)
                {
                    DnsCryptToggle.IsChecked = false;
                    if (DnsCryptSplitView != null) DnsCryptSplitView.IsPaneOpen = false;
                }

                if (MainSplitView != null) MainSplitView.IsPaneOpen = true;
            }
            else
            {
                if (MainSplitView != null) MainSplitView.IsPaneOpen = false;
            }
        }

        private void SecurityToggle_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            bool isOpen = SecurityToggle.IsChecked ?? false;

            if (isOpen)
            {
                if (MaintenanceToggle.IsChecked == true)
                {
                    MaintenanceToggle.IsChecked = false;
                    if (MainSplitView != null) MainSplitView.IsPaneOpen = false;
                }

                if (DnsCryptToggle != null && DnsCryptToggle.IsChecked == true)
                {
                    DnsCryptToggle.IsChecked = false;
                    if (DnsCryptSplitView != null) DnsCryptSplitView.IsPaneOpen = false;
                }

                if (SecuritySplitView != null) SecuritySplitView.IsPaneOpen = true;

                ViewModel.InitializeSecurityScan();
            }
            else
            {
                if (SecuritySplitView != null) SecuritySplitView.IsPaneOpen = false;
            }
        }
        #endregion

        #region Helper Methods
        private void ResetUIState()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (RepairStatusText != null) RepairStatusText.Text = ResourceString.GetString("StatusTextBlockDefault") ?? "Ready";
                if (RepairProgressBar != null) RepairProgressBar.Value = 0;
                if (StopButton != null)
                {
                    StopButton.Visibility = Visibility.Collapsed;
                    StopButton.IsEnabled = true;
                }
                if (ScanRepairPanel != null) ScanRepairPanel.Visibility = Visibility.Visible;
                if (RepairPercentageText != null) RepairPercentageText.Text = string.Empty;
            });
        }

        private async Task ShowScanResultsDialogAsync(List<string> selectedNames)
        {
            var stackPanel = new StackPanel { Spacing = 8 };

            foreach (var name in selectedNames)
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"{name}:",
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Margin = new Thickness(0, 8, 0, 4)
                });
                stackPanel.Children.Add(new TextBlock
                {
                    Text = _scanResults[name].ToString().Trim(),
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                });
            }

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
                Title = ResourceString.GetString("ScanResults") ?? "Scan Results",
                Content = new ScrollViewer
                {
                    Content = stackPanel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    MaxHeight = 350
                },
                CloseButtonText = ResourceString.GetString("Close") ?? "Close",
                DefaultButton = ContentDialogButton.Close
            };

            await dialog.ShowAsync();
        }

        private void SetFocusTo(object element)
        {
            if (element is Control control && control.IsEnabled && control.Visibility == Visibility.Visible)
            {
                control.Focus(FocusState.Programmatic);
            }
        }

        private void UpdateDriverButtonStates()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                bool isInstalled = HardwareDriverHelper.IsPawnIoInstalled();

                if (BtnInstallDriver != null) BtnInstallDriver.IsEnabled = !isInstalled;
                if (BtnUninstallDriver != null) BtnUninstallDriver.IsEnabled = isInstalled;
            });
        }
        #endregion

        #region Health Status Calculator
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await CalculateSystemHealthAsync();
        }

        private async Task CalculateSystemHealthAsync()
        {
            if (ViewModel == null) return;

            if (MaintenanceStatusLoadingRing != null)
            {
                MaintenanceStatusLoadingRing.IsActive = true;
                MaintenanceStatusLoadingRing.Visibility = Visibility.Visible;
            }
            if (MaintenanceStatusImage != null) MaintenanceStatusImage.Visibility = Visibility.Collapsed;
            if (this.FindName("LastRefreshedText") is TextBlock text) text.Text = string.Empty;
            if (this.FindName("RefreshButton") is Button refreshBtn) refreshBtn.IsEnabled = false;

            await Task.Delay(150);

            if (ViewModel.RefreshCleanupSpaceCommand.CanExecute(null) && !ViewModel.IsScanning)
            {
                ViewModel.RefreshCleanupSpaceCommand.Execute(null);
                await Task.Delay(400);
            }

            while (ViewModel.IsScanning)
            {
                await Task.Delay(250);
            }

            var healthResult = await SystemHealthHelper.EvaluateHealthAsync();

            try
            {
                if (MaintenanceStatusImage != null)
                {
                    string pathStr = healthResult.ImagePath;
                    Uri imageUri = pathStr.StartsWith("ms-appx://", StringComparison.OrdinalIgnoreCase) ||
                                   pathStr.StartsWith("ms-appdata://", StringComparison.OrdinalIgnoreCase)
                        ? new Uri(pathStr)
                        : new Uri($"ms-appx:///{pathStr.TrimStart('/')}");

                    MaintenanceStatusImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(imageUri);
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"Failed to load health image: {ex.Message}");
            }

            if (this.FindName("LastRefreshedText") is TextBlock lbl)
            {
                lbl.Text = $"{healthResult.StatusText} • Last checked: {DateTime.Now:t}";
                lbl.Visibility = Visibility.Visible;
            }

            if (MaintenanceStatusLoadingRing != null)
            {
                MaintenanceStatusLoadingRing.IsActive = false;
                MaintenanceStatusLoadingRing.Visibility = Visibility.Collapsed;
            }
            if (MaintenanceStatusImage != null) MaintenanceStatusImage.Visibility = Visibility.Visible;
            if (this.FindName("RefreshButton") is Button rBtn) rBtn.IsEnabled = true;
        }

        #endregion

        #region Purge Page
        public async Task Purge()
        {
            _isCurrentPageActive = false;
            _navGeneration++;

            try
            {
                Debug.WriteLine("[DiagnosticsPage] Singleton Purge Initiated...");

                await StopCurrentOperationAsync();

                if (_cancellationTokenSource != null)
                {
                    try { _cancellationTokenSource.Cancel(); _cancellationTokenSource.Dispose(); } catch { }
                    _cancellationTokenSource = null;
                }

                ViewModel?.PauseUiUpdates();

                if (!SettingsEngine.IsHighPerformanceModeEnabled)
                {
                    Debug.WriteLine("[DiagnosticsPage] Low Resource Mode: Evicting heavy UI buffers...");

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(350);

                        DispatcherQueue?.TryEnqueue(() =>
                        {
                            if (ViewModel != null)
                            {
                                ViewModel.MainGraphValues.Clear();
                                ViewModel.MainGraphDot.Clear();
                                ViewModel.AltGraphValues.Clear();
                                ViewModel.AltGraphDot.Clear();
                                ViewModel.TempGraphValues.Clear();
                                ViewModel.TempGraphDot.Clear();

                                Debug.WriteLine("[DiagnosticsPage] Severed main window Graph Paths. Tray collections preserved.");
                            }

                            foreach (var result in _scanResults.Values)
                            {
                                result.Clear();
                            }

                            _controls.Clear();

                            this.Bindings?.StopTracking();
                            this.DataContext = null;
                            this.Content = null;

                            _isInitialized = false;
                        });

                        GC.Collect(2, GCCollectionMode.Optimized, false, false);
                        ViewModel?.ForceImmediateMemoryCleanup();
                    });
                }
                else
                {
                    Debug.WriteLine("[DiagnosticsPage] Bypass Purge: High Performance Mode is ON.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DiagnosticsPage] Error during singleton purge: {ex.Message}");
            }
        }
        #endregion

        #region DNSCrypt Logic

        public async void UpdateDnsCryptControls()
        {
            bool isInstalled = await Task.Run(() => DNSCryptHelper.IsInstalled());
            bool isRunning = await Task.Run(() => DNSCryptHelper.IsRunning());
            string config = isInstalled ? await Task.Run(() => DNSCryptHelper.LoadConfig()) : string.Empty;

            DispatcherQueue?.TryEnqueue(() =>
            {
                BtnDownloadInstall.IsEnabled = true;
                BtnStartService.Content = "Start service";
                BtnStartService.IsEnabled = true;
                statusLabel.Text = "Nothing is running in the background";
                ProgressRingRunServices.Visibility = Visibility.Collapsed;
                TxtServicesRunning.Text = "";
                IconServiceStopped.Visibility = Visibility.Visible;
                ImgServiceRunning.Visibility = Visibility.Collapsed;
                BtnOpenConfigFile.IsEnabled = true;
                BtnDebug.IsEnabled = true;

                foreach (var pair in _controls) pair.Value.IsEnabled = true;

                BtnBalanced.IsEnabled = true;
                BtnPrivacy.IsEnabled = true;
                BtnSaveConfig.IsEnabled = true;
                BtnLoadConfig.IsEnabled = true;

                if (!isInstalled)
                {
                    BtnStartService.IsEnabled = false;
                    BtnOpenConfigFile.IsEnabled = false;
                    BtnDebug.IsEnabled = false;

                    foreach (var pair in _controls) pair.Value.IsEnabled = false;

                    BtnBalanced.IsEnabled = false;
                    BtnPrivacy.IsEnabled = false;
                    BtnSaveConfig.IsEnabled = false;
                    BtnLoadConfig.IsEnabled = false;

                    string install = ResourceString.GetString("btn_download_install") ?? "Install";
                    ToolTipService.SetToolTip(BtnDownloadInstall, install);
                    IconDownloadInstall.Glyph = "\uE896";
                    IconDownloadInstall.ClearValue(FontIcon.ForegroundProperty);
                    AnimateInstallButton();
                }
                else
                {
                    string uninstall = ResourceString.GetString("btn_uninstall_script") ?? "Uninstall";
                    ToolTipService.SetToolTip(BtnDownloadInstall, uninstall);
                    IconDownloadInstall.Glyph = "\uE74D";
                    IconDownloadInstall.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);

                    ((Microsoft.UI.Xaml.Media.TranslateTransform)((Microsoft.UI.Xaml.Media.TransformGroup)BtnDownloadInstall.RenderTransform).Children[0]).Y = 0;

                    if (isRunning)
                    {
                        statusLabel.Text = "DNSCrypt Service is running.";
                        ProgressRingRunServices.Visibility = Visibility.Visible;
                        TxtServicesRunning.Text = ResourceString.GetString("text_services_running") ?? "Running";

                        IconServiceStopped.Visibility = Visibility.Collapsed;
                        ImgServiceRunning.Visibility = Visibility.Visible;

                        BtnDownloadInstall.IsEnabled = false;
                        BtnStartService.Content = "Stop service";
                        BtnOpenConfigFile.IsEnabled = false;
                        BtnDebug.IsEnabled = false;

                        foreach (var pair in _controls) pair.Value.IsEnabled = false;

                        BtnBalanced.IsEnabled = false;
                        BtnPrivacy.IsEnabled = false;
                        BtnSaveConfig.IsEnabled = false;
                        BtnLoadConfig.IsEnabled = false;
                    }

                    if (!string.IsNullOrEmpty(config))
                    {
                        foreach (var pair in _controls)
                        {
                            var currentSetting = pair.Key.GetCurrentSetting(config);
                            var settings = pair.Key.GetSettings(config);

                            pair.Value.Items.Clear();
                            var selectedItem = (object?)null;

                            foreach (var item in settings)
                            {
                                pair.Value.Items.Add(item);
                                if ((string)item.Value == currentSetting) selectedItem = item;
                            }

                            if (selectedItem != null) pair.Value.SelectedItem = selectedItem;
                            else pair.Value.SelectedIndex = 0;
                        }
                    }
                }

                // Force WinUI 3 to instantly redraw the open SplitView
                this.UpdateLayout();
            });
        }

        private void AnimateInstallButton()
        {
            if (!DNSCryptHelper.IsInstalled())
            {
                // FactoryAnimation.ButtonBounce(BtnDownloadInstall, 20, animationDurationSeconds: 0.25);
            }
        }

        private async void BtnDownloadInstall_Click(object sender, RoutedEventArgs e)
        {
            bool isInstalled = DNSCryptHelper.IsInstalled();

            BtnDownloadInstall.IsEnabled = false;

            try
            {
                if (!isInstalled)
                {
                    bool isConnected = await Task.Run(() => NetworkHelper.IsConnectedAsync());
                    if (!isConnected)
                    {
                        return;
                    }
                }

                if (isInstalled)
                {
                    DNSCryptHelper.Uninstall(progressBar, statusLabel);
                    ClearComboBoxes();
                }
                else
                {
                    await DNSCryptHelper.Install(progressBar, statusLabel);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DNSCrypt] Operation failed: {ex.Message}");
            }
            finally
            {
                UpdateDnsCryptControls();
                AnimateInstallButton();
                BtnDownloadInstall.IsEnabled = true;
            }
        }

        private async void BtnOpenConfigFile_Click(object sender, RoutedEventArgs e)
        {
            BtnOpenConfigFile.IsEnabled = false;
            await DNSCryptHelper.OpenConfig();
            BtnOpenConfigFile.IsEnabled = true;
        }

        private async void BtnStartService_Click(object sender, RoutedEventArgs e)
        {
            BtnStartService.IsEnabled = false;

            try
            {
                if (DNSCryptHelper.IsRunning())
                {
                    await DNSCryptHelper.StopService(progressBar, statusLabel);
                    ProgressRingRunServices.Visibility = Visibility.Collapsed;
                    TxtServicesRunning.Text = "";

                    IconServiceStopped.Visibility = Visibility.Visible;
                    ImgServiceRunning.Visibility = Visibility.Collapsed;
                }
                else
                {
                    UpdateDnsCryptControls();

                    BtnSaveConfig_Click(BtnSaveConfig, null!);

                    await DNSCryptHelper.StartService(progressBar, statusLabel);
                    ProgressRingRunServices.Visibility = Visibility.Visible;
                    TxtServicesRunning.Text = ResourceString.GetString("text_services_running");

                    IconServiceStopped.Visibility = Visibility.Collapsed;
                    ImgServiceRunning.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DNSCrypt Service] Error: {ex.Message}");
                statusLabel.Text = "Service action failed.";
            }
            finally
            {
                UpdateDnsCryptControls();
                BtnStartService.IsEnabled = true;
            }
        }

        private async void BtnDebug_Click(object sender, RoutedEventArgs e)
        {
            BtnDebug.IsEnabled = false;

            try
            {
                bool isConnected = await Task.Run(() => NetworkHelper.IsConnectedAsync());

                if (!isConnected)
                {
                    statusLabel.Text = "Connection failed.";
                    return;
                }

                await DNSCryptHelper.DebugProcess(progressBar, statusLabel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DNSCrypt Debug] Error: {ex.Message}");
            }
            finally
            {
                BtnDebug.IsEnabled = true;
            }
        }

        private void BtnBalanced_Click(object sender, RoutedEventArgs e)
        {
            foreach (var pair in _controls)
            {
                if (pair.Value.SelectedItem == null) continue;

                var setting = (Structs.ComboBoxItem)pair.Value.SelectedItem;
                var targetSetting = pair.Key.GetSetting();

                if ((string)setting.Value != targetSetting)
                {
                    foreach (var item in pair.Value.Items)
                    {
                        var currentItem = (Structs.ComboBoxItem)item;

                        if ((string)currentItem.Value == targetSetting)
                        {
                            pair.Value.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }

        private void BtnPrivacy_Click(object sender, RoutedEventArgs e)
        {
            foreach (var pair in _controls)
            {
                if (pair.Value.SelectedItem == null) continue;

                var setting = (Structs.ComboBoxItem)pair.Value.SelectedItem;
                var targetSetting = pair.Key.GetSetting(DNSSettingPreference.Privacy);

                if ((string)setting.Value != targetSetting)
                {
                    foreach (var item in pair.Value.Items)
                    {
                        var currentItem = (Structs.ComboBoxItem)item;

                        if ((string)currentItem.Value == targetSetting)
                        {
                            pair.Value.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            var config = DNSCryptHelper.LoadConfig();

            foreach (var pair in _controls)
            {
                if (pair.Value.SelectedItem != null)
                {
                    var setting = (Structs.ComboBoxItem)pair.Value.SelectedItem;
                    config = pair.Key.SetSetting(config, (string)setting.Value);
                }
            }

            DNSCryptHelper.SaveConfig(config);
        }

        private void BtnLoadConfig_Click(object sender, RoutedEventArgs e)
        {
            UpdateDnsCryptControls();
        }

        private void ClearComboBoxes()
        {
            foreach (var control in _controls.Values)
            {
                control.SelectedItem = null;
            }
        }
        #endregion
    }
}