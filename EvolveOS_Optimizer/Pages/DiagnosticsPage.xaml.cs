// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License. 

using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Input;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class DiagnosticsPage : Page, IPurgeable
    {
        public DiagnosticsPageViewModel? ViewModel { get; } = DiagnosticsPageViewModel.Current;

        private bool _isCurrentPageActive = false;

        public static string RequestedPaneOnLoad = "";
        public static Action<string>? ExternalPaneRequest;

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
            this.DataContext = ViewModel;

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
            ExternalPaneRequest = SwitchToPane;

            if (!string.IsNullOrEmpty(RequestedPaneOnLoad))
            {
                SwitchToPane(RequestedPaneOnLoad);
                RequestedPaneOnLoad = "";
            }

            var vm = ViewModel;

            /*if (vm != null && !vm.IsScanning)
            {
                await vm.ExecuteFullScanAsync();
            }*/

            vm?.ResumeUiUpdates();

            if (vm?.SelectedProcess == null && vm?.Processes.Count > 0)
            {
                vm.SelectedProcess = vm.Processes[0];
            }

            await CalculateSystemHealthAsync();

            if (!string.IsNullOrEmpty(_pendingScrollTarget))
            {
                await ScrollToElementHelper.ScrollToElementAsync(this, _pendingScrollTarget);
                _pendingScrollTarget = null;
            }
        }

        private void DiagnosticsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                RadarSpinStoryboard?.Stop();
            }

            ExternalPaneRequest = null;
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
        }

        private async void ViewModel_ShowSecurityIssuesRequested(List<string> issues)
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
            if (vm != null && sender is Button button && button.DataContext is EvolveOS_Optimizer.Core.Model.HardwareIssue selectedIssue)
            {
                await vm.FixHardwareAsync(selectedIssue);
            }
        }

        private async void StartFullScan_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel != null && !ViewModel.IsScanning)
            {
                await ViewModel.ExecuteFullScanAsync();
                await CalculateSystemHealthAsync();
            }
        }

        private async void Expander_Expanding(Microsoft.UI.Xaml.Controls.Expander sender, Microsoft.UI.Xaml.Controls.ExpanderExpandingEventArgs args)
        {
            await Task.Delay(150);

            sender.StartBringIntoView(new BringIntoViewOptions
            {
                AnimationDesired = true
            });
        }
        #endregion

        #region Callbacks (Maintenance)
        private void OnRemoveProcessFromExclusionListCommandCompletedCallback()
        {
            if (this.FindName("ProcessExclusionList") is Control ctrl) SetFocusTo(ctrl);
        }
        #endregion

        #region Navigation & Lifecycle
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            this.Bindings.Initialize();
            this.Bindings.Update();

            _isCurrentPageActive = true;

            if (e.Parameter is string optionTag && !string.IsNullOrEmpty(optionTag))
            {
                _pendingScrollTarget = optionTag;
            }

            if (ViewModel != null)
            {
                ViewModel.PropertyChanged += ViewModel_PropertyChanged;
                ViewModel.ShowSecurityIssuesRequested += ViewModel_ShowSecurityIssuesRequested;
                ViewModel.CloseActiveDialogsRequested += ViewModel_CloseActiveDialogsRequested;
                ViewModel.OnAddProcessToExclusionListCommandCompleted += OnAddProcessToExclusionListCommandCompleted;
                ViewModel.OnRemoveProcessFromExclusionListCommandCompleted += OnRemoveProcessFromExclusionListCommandCompletedCallback;
                ViewModel.OnOptimizeCommandCompleted += OnOptimizeCommandCompleted;

                if (ViewModel.HardwareScannerVisibility == Visibility.Visible) HeartbeatStoryboard?.Begin();
                if (ViewModel.ScanningVisibility == Visibility.Visible) SystemSonarStoryboard?.Begin();
                if (ViewModel.EventEmptyStateVisibility == Visibility.Visible) RadarSpinStoryboard?.Begin();
            }

            ViewModel?.ResumeUiUpdates();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _isCurrentPageActive = false;

            ViewModel?.PauseUiUpdates();

            SystemSonarStoryboard?.Stop();
            HeartbeatStoryboard?.Stop();
            RadarSpinStoryboard?.Stop();

            this.Bindings.StopTracking();

            if (ViewModel != null)
            {
                ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
                ViewModel.ShowSecurityIssuesRequested -= ViewModel_ShowSecurityIssuesRequested;
                ViewModel.CloseActiveDialogsRequested -= ViewModel_CloseActiveDialogsRequested;
                ViewModel.OnAddProcessToExclusionListCommandCompleted -= OnAddProcessToExclusionListCommandCompleted;
                ViewModel.OnRemoveProcessFromExclusionListCommandCompleted -= OnRemoveProcessFromExclusionListCommandCompletedCallback;
                ViewModel.OnOptimizeCommandCompleted -= OnOptimizeCommandCompleted;
            }

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
                await ViewModel.Optimize(Enums.Memory.Optimization.Reason.Manual);
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
            selectedCount = 0;
            if (DismCheckBox?.IsChecked == true) selectedCount++;
            if (SfcCheckBox?.IsChecked == true) selectedCount++;
            if (ChkdskCheckBox?.IsChecked == true) selectedCount++;
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

        private void OnOptimizeCommandCompleted(Enums.Memory.Optimization.Reason reason, string message)
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
        #endregion

        #region Health Status Calculator
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await CalculateSystemHealthAsync();
        }

        private async Task CalculateSystemHealthAsync()
        {
            if (ViewModel == null) return;

            if (MaintenanceStatusLoadingRing != null) MaintenanceStatusLoadingRing.Visibility = Visibility.Visible;
            if (MaintenanceStatusImage != null) MaintenanceStatusImage.Visibility = Visibility.Collapsed;
            if (this.FindName("LastRefreshedText") is TextBlock text) text.Text = string.Empty;
            if (this.FindName("RefreshButton") is Button refreshBtn) refreshBtn.IsEnabled = false;

            if (ViewModel.RefreshCleanupSpaceCommand.CanExecute(null))
            {
                ViewModel.RefreshCleanupSpaceCommand.Execute(null);
            }

            while (ViewModel.IsScanning)
            {
                await Task.Delay(250);
            }

            double ramPercentage = ViewModel.Computer?.Memory?.Physical?.Used?.Percentage ?? 0;
            double totalRamGb = ViewModel.Computer?.Memory?.Physical?.Total?.Gigabytes ?? 16.0;

            double vRamPercentage = ViewModel.Computer?.Memory?.Virtual?.Used?.Percentage ?? 0;
            double totalVRamGb = ViewModel.Computer?.Memory?.Virtual?.Total?.Gigabytes ?? 16.0;

            double junkGigabytes = ParseSizeToGigabytes(ViewModel.TotalSpaceToFree);

            var healthResult = SystemHealthHelper.EvaluateHealth(
                ramPercentage, totalRamGb,
                vRamPercentage, totalVRamGb,
                junkGigabytes);

            try
            {
                if (MaintenanceStatusImage != null)
                {
                    MaintenanceStatusImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(healthResult.ImagePath));
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

            if (MaintenanceStatusLoadingRing != null) MaintenanceStatusLoadingRing.Visibility = Visibility.Collapsed;
            if (MaintenanceStatusImage != null) MaintenanceStatusImage.Visibility = Visibility.Visible;
            if (this.FindName("RefreshButton") is Button rBtn) rBtn.IsEnabled = true;
        }

        private double ParseSizeToGigabytes(string sizeString)
        {
            if (string.IsNullOrWhiteSpace(sizeString)) return 0;

            string cleanString = sizeString.ToUpper().Replace(",", ".");
            var match = Regex.Match(cleanString, @"([\d\.]+)\s*(GB|MB|KB|B)");

            if (match.Success)
            {
                if (double.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out double value))
                {
                    string unit = match.Groups[2].Value;
                    return unit switch
                    {
                        "GB" => value,
                        "MB" => value / 1024.0,
                        "KB" => value / 1048576.0,
                        "B" => value / 1073741824.0,
                        _ => 0
                    };
                }
            }
            return 0;
        }
        #endregion

        #region Purge Page
        public async void Purge()
        {
            try
            {
                Debug.WriteLine("[DiagnosticsPage] Purging background tasks...");

                await StopCurrentOperationAsync();

                if (_cancellationTokenSource != null)
                {
                    try { _cancellationTokenSource.Cancel(); _cancellationTokenSource.Dispose(); } catch { }
                    _cancellationTokenSource = null;
                }

                Debug.WriteLine("[DiagnosticsPage] Tasks cleaned. Collections preserved for Cache safety.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DiagnosticsPage] Error during purge: {ex.Message}");
            }
        }
        #endregion
    }
}