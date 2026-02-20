using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using CommunityToolkit.WinUI;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class MaintenancePage : Page
    {
        #region Fields
        private readonly MaintenanceViewModel? _viewModel;
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
        #endregion

        #region Constructor & Initialization
        public MaintenancePage()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;

            IComputerService computerService = new ComputerService();
            IHotkeyService? globalHotkeyService = App.GetService<IHotkeyService>();

            _viewModel = new MaintenanceViewModel(computerService, globalHotkeyService!);
            this.DataContext = _viewModel;

            this.Loaded += MaintenancePage_Loaded;

            if (_viewModel != null)
            {
                _viewModel.OnAddProcessToExclusionListCommandCompleted += OnAddProcessToExclusionListCommandCompleted;
                _viewModel.OnRemoveProcessFromExclusionListCommandCompleted += () => SetFocusTo(ProcessExclusionList);

                _viewModel.OnOptimizeCommandCompleted -= OnOptimizeCommandCompleted;
                _viewModel.OnOptimizeCommandCompleted += OnOptimizeCommandCompleted;
            }

            this.Unloaded += MaintenancePage_Unloaded;
        }

        private async void MaintenancePage_Loaded(object sender, RoutedEventArgs e)
        {
            Optimize.Focus(FocusState.Programmatic);

            await CalculateSystemHealthAsync();

            if (!string.IsNullOrEmpty(_pendingScrollTarget))
            {
                await ScrollToElementHelper.ScrollToElementAsync(this, _pendingScrollTarget);
                _pendingScrollTarget = null;
            }
        }
        #endregion

        #region Navigation & Lifecycle
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is string optionTag && !string.IsNullOrEmpty(optionTag))
            {
                _pendingScrollTarget = optionTag;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
        }

        private void MaintenancePage_Unloaded(object sender, RoutedEventArgs e)
        {
        }
        #endregion

        #region Command Line & System Tools Processing
        private async Task RunCommandsAsync(bool isRepair)
        {
            ScanRepairPanel.Visibility = Visibility.Collapsed;
            StopButton.Visibility = Visibility.Visible;
            StopButton.IsEnabled = true;
            ProgressBar.Value = 0;
            _currentProcessId = 0;

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var ct = _cancellationTokenSource.Token;

            var commands = new[]
            {
                (DismCheckBox, "DISM", isRepair ? "/Online /Cleanup-Image /RestoreHealth" : "/Online /Cleanup-Image /ScanHealth"),
                (SfcCheckBox, "SFC", isRepair ? "/scannow" : "/verifyonly"),
                (ChkdskCheckBox, "CHKDSK", isRepair ? "/f" : "")
            };

            var current = 0;
            var selectedNames = new List<string>();
            var wasCancelled = false;
            var hasError = false;

            try
            {
                foreach (var (checkBox, name, args) in commands)
                {
                    if (ct.IsCancellationRequested)
                    {
                        wasCancelled = true;
                        break;
                    }

                    if (checkBox.IsChecked == true)
                    {
                        current++;
                        selectedNames.Add(name);

                        string formatString = isRepair
                            ? ResourceString.GetString("RepairInProgress")
                            : ResourceString.GetString("ScanInProgress");

                        StatusTextBlock.Text = string.Format(formatString, current, selectedCount, name);
                        ProgressBar.Value = 0;

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

                            if (ct.IsCancellationRequested)
                            {
                                wasCancelled = true;
                                break;
                            }
                        }
                    }
                }
            }
            finally
            {
                _currentProcessId = 0;
                ResetUIState();

                if (wasCancelled)
                {
                    App.ShowNotification(ResourceString.GetString("Repair"), ResourceString.GetString("OperationStopped"), InfoBarSeverity.Error, 5000);
                }
                else if (hasError)
                {
                    App.ShowNotification(ResourceString.GetString("Repair"), ResourceString.GetString("UnexpectedError"), InfoBarSeverity.Error, 5000);

                    if (selectedNames.Count > 0)
                    {
                        await ShowScanResultsDialogAsync(selectedNames);
                    }
                }
                else
                {
                    App.ShowNotification(ResourceString.GetString("Repair"), ResourceString.GetString("OperationCompleted"), InfoBarSeverity.Success, 5000);

                    if (selectedNames.Count > 0)
                    {
                        await ShowScanResultsDialogAsync(selectedNames);
                    }
                }
            }
        }

        private async Task RunCommandAsync(string name, string args, CancellationToken ct)
        {
            _scanResults[name].Clear();

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

            ErrorLogging.LogDebug($"Output: {line}");
            HandleOutputLine(name, line);
        }

        private void HandleOutputLine(string name, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            UpdateProgress(name, line);

            var isProgress = name switch
            {
                "DISM" => Regex.IsMatch(line, @"\[\s*[= ]*\s*(\d+(\.\d+)?)%\s*[= ]*\]"),
                "SFC" => Regex.IsMatch(line, @"^\s*(\d+)\s*%\s*$"),
                "CHKDSK" => Regex.IsMatch(line, @"Total:\s*(\d+)%", RegexOptions.IgnoreCase),
                _ => false
            };

            if (isProgress)
            {
                return;
            }

            if (name == "SFC" || name == "DISM")
            {
                _scanResults[name].Clear();
                _scanResults[name].AppendLine(line);
            }
            else
            {
                _scanResults[name].AppendLine(line);
            }
        }

        private void UpdateProgress(string commandName, string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return;
            }

            var percentage = 0;

            try
            {
                if (commandName == "DISM")
                {
                    var match = Regex.Match(data, @"\[\s*[= ]*\s*(\d+(\.\d+)?)%\s*[= ]*\]");
                    if (match.Success)
                    {
                        percentage = (int)Math.Round(double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
                    }
                }
                else if (commandName == "SFC")
                {
                    var match = Regex.Match(data, @"(\d+)%", RegexOptions.IgnoreCase);

                    if (match.Success)
                    {
                        percentage = int.Parse(match.Groups[1].Value);
                    }
                }
                else if (commandName == "CHKDSK")
                {
                    var match = Regex.Match(data, @"Total:\s*(\d+)%", RegexOptions.IgnoreCase);

                    if (match.Success)
                    {
                        percentage = int.Parse(match.Groups[1].Value);
                    }
                }

                if (percentage > 0 && percentage <= 100)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        ProgressBar.Value = percentage;
                        PercentageTextBlock.Text = $"{percentage}%";
                    });
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
            if (string.IsNullOrEmpty(winDir))
            {
                return toolExecutable;
            }

            if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess)
            {
                var sysNativePath = Path.Combine(winDir, "SysNative", toolExecutable);
                if (File.Exists(sysNativePath))
                {
                    return sysNativePath;
                }
            }

            var system32Path = Path.Combine(winDir, "System32", toolExecutable);
            if (File.Exists(system32Path))
            {
                return system32Path;
            }

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
                catch
                {
                    // Ignore errors during cleanup
                }
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

        private async void OnStopButtonClick(object sender, RoutedEventArgs e)
        {
            StopButton.IsEnabled = false;

            await StopCurrentOperationAsync();
        }

        private async void BatteryHealthButton_Click(object sender, RoutedEventArgs e)
        {
            var reportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "batteryreport.html");

            var command = $"%SystemRoot%\\System32\\powercfg.exe /batteryreport /output \"{reportPath}\"";

            var result = await CommandExecutor.StartInCmd(command);

            if (result == 0 && File.Exists(reportPath))
            {
                App.ShowNotification(ResourceString.GetString("BatteryStatus"), ResourceString.GetString("ReportSaved"), InfoBarSeverity.Success, 5000);
                return;
            }
            App.ShowNotification(ResourceString.GetString("BatteryStatus"), ResourceString.GetString("UnexpectedError"), InfoBarSeverity.Error, 5000);
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
                Title = ResourceString.GetString("MemoryDiagnosticDialogTitle"),
                Content = ResourceString.GetString("MemoryDiagnosticDialogText"),
                PrimaryButtonText = ResourceString.GetString("RestartNow"),
                SecondaryButtonText = ResourceString.GetString("ScheduleLater"),
                CloseButtonText = ResourceString.GetString("Cancel")
            };

            memDialog.PrimaryButtonClick += async (sender, args) =>
            {
                await CommandExecutor.StartInCmd("bcdedit /bootsequence {memdiag} && shutdown /r /t 0");
            };

            memDialog.SecondaryButtonClick += async (sender, args) =>
            {
                App.ShowNotification(ResourceString.GetString("MemoryDiagnosticDialogTitle"), ResourceString.GetString("ScheduledLater"), InfoBarSeverity.Success, 5000);
                MemCheckButton.IsEnabled = false;
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
            foreach (var checkbox in CheckBoxes.Children)
            {
                if (checkbox is CheckBox checkBox && checkBox.IsChecked == true)
                {
                    selectedCount++;
                }
            }
        }

        private void OnSliderPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Slider slider)
            {
                slider.Focus(FocusState.Pointer);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            NavigateBack();
        }
        #endregion

        #region View Model Callbacks
        private void OnAddProcessToExclusionListCommandCompleted()
        {
            Debug.WriteLine("Process added to exclusion list.");
        }

        private void OnRemoveProcessFromExclusionListCommandCompleted()
        {
            ProcessExclusionList.Focus(FocusState.Programmatic);
        }

        private void OnOptimizeCommandCompleted(Enums.Memory.Optimization.Reason reason, string message)
        {
            if (_isShowingResult) return;

            _isShowingResult = true;

            if (LocalMachineSettingsEngine.DisableAllOptimizationResults)
            {
                _isShowingResult = false;
                return;
            }

            this.DispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    if (LocalMachineSettingsEngine.RestartExplorerAfterOptimization)
                    {
                        await Task.Delay(2000);
                    }

                    if (!LocalMachineSettingsEngine.ShowOptimizationNotifications)
                    {
                        var dialog = new ContentDialog
                        {
                            XamlRoot = this.XamlRoot,
                            Title = ResourceString.GetString("title_optimization_result"),
                            Content = message,
                            CloseButtonText = ResourceString.GetString("btn_ok") ?? "OK",
                            DefaultButton = ContentDialogButton.Close
                        };
                        await dialog.ShowAsync();
                    }
                    else
                    {
                        NotificationManager.Show(message).Perform();
                    }
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
        #endregion

        #region Helper Methods
        private void ResetUIState()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusTextBlock.Text = "StatusTextBlockDefault".GetLocalized();
                ProgressBar.Value = 0;
                StopButton.Visibility = Visibility.Collapsed;
                StopButton.IsEnabled = true;
                ScanRepairPanel.Visibility = Visibility.Visible;
                PercentageTextBlock.Text = string.Empty;
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
                Title = "ScanResults".GetLocalized(),
                Content = new ScrollViewer
                {
                    Content = stackPanel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    MaxHeight = 350
                },
                CloseButtonText = "Close".GetLocalized()
            };

            await dialog.ShowAsync();
        }

        private void NavigateBack()
        {
            if (this.Frame != null && this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }

        private void SetFocusTo(object element)
        {
            if (element is Control control && control.IsEnabled && control.Visibility == Visibility.Visible)
            {
                control.Focus(FocusState.Programmatic);
            }
        }

        #region Health Status Calculator

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await CalculateSystemHealthAsync();
        }

        private async Task CalculateSystemHealthAsync()
        {
            if (_viewModel == null) return;

            MaintenanceStatusLoadingRing.Visibility = Visibility.Visible;
            MaintenanceStatusImage.Visibility = Visibility.Collapsed;
            LastRefreshedText.Visibility = Visibility.Collapsed;
            RefreshButton.IsEnabled = false;

            if (_viewModel.RefreshCleanupSpaceCommand.CanExecute(null))
            {
                _viewModel.RefreshCleanupSpaceCommand.Execute(null);
            }

            while (_viewModel.IsScanning)
            {
                await Task.Delay(250);
            }

            int penaltyScore = 0;

            double ramUsage = _viewModel.Computer?.Memory?.Physical?.Used?.Percentage ?? 0;
            if (ramUsage > 85) penaltyScore += 2;
            else if (ramUsage > 70) penaltyScore += 1;

            double vRamUsage = _viewModel.Computer?.Memory?.Virtual?.Used?.Percentage ?? 0;
            if (vRamUsage > 85) penaltyScore += 2;
            else if (vRamUsage > 70) penaltyScore += 1;

            double junkGigabytes = ParseSizeToGigabytes(_viewModel.TotalSpaceToFree);
            if (junkGigabytes > 5.0) penaltyScore += 2;
            else if (junkGigabytes > 1.0) penaltyScore += 1;

            string imagePath;
            string statusText;

            if (penaltyScore >= 4)
            {
                imagePath = "ms-appx:///Assets/PngImages/health_critical.png";
                statusText = ResourceString.GetString("Health_Poor") ?? "Poor - Action Required";
            }
            else if (penaltyScore >= 2)
            {
                imagePath = "ms-appx:///Assets/PngImages/health_warning.png";
                statusText = ResourceString.GetString("Health_Warning") ?? "Fair - Optimization Recommended";
            }
            else
            {
                imagePath = "ms-appx:///Assets/PngImages/health_good.png";
                statusText = ResourceString.GetString("Health_Good") ?? "Good - System is Healthy";
            }

            try
            {
                MaintenanceStatusImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(imagePath));
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug($"Failed to load health image: {ex.Message}");
            }

            LastRefreshedText.Text = $"{statusText} • Last checked: {DateTime.Now:t}";

            MaintenanceStatusLoadingRing.Visibility = Visibility.Collapsed;
            MaintenanceStatusImage.Visibility = Visibility.Visible;
            LastRefreshedText.Visibility = Visibility.Visible;
            RefreshButton.IsEnabled = true;
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

        #endregion
    }
}