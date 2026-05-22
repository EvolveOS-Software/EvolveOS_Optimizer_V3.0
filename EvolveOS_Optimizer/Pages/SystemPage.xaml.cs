// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Tweaks;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Win32;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SystemPage : Page, IPurgeable
    {
        private SystemTweaks? _sysTweaks = new SystemTweaks();
        private const string RegistryBaseKey = @"SOFTWARE\EvolveOS_Optimizer\SystemOptimizations";
        private bool _isInitializingPowerMode;
        private bool _isInitializingWindowsUpdates;

        public SystemPage()
        {
            this.InitializeComponent();

            if (SettingsEngine.IsHighPerformanceModeEnabled)
            {
                this.NavigationCacheMode = NavigationCacheMode.Required;
            }
            else
            {
                this.NavigationCacheMode = NavigationCacheMode.Disabled;
            }

            this.DataContext = new SystemViewModel();

            Loaded += SystemPage_Loaded;
            Unloaded += SystemPage_Unloaded;
        }

        private async void SystemPage_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializePowerModeAsync();
            await InitializeWindowsUpdatesAsync();

            //DebugAvailableCards();

            var vm = new SystemViewModel();
            this.DataContext = vm;

            vm.UpdateCounters();
            vm.ApplyRecommendations();
        }

        private void SystemPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Purge();
        }

        #region Toggles & Sliders Logic

        private async void NativeTgl_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch tgl)
            {
                if (!tgl.IsLoaded) return;

                if (tgl.DataContext is SystemModel model)
                {
                    if (tgl.IsOn == model.State) return;

                    string key = model.Name;

                    bool isOn = tgl.IsOn;
                    bool isDisabled = isOn;

                    model.State = isOn;

                    //Debug.WriteLine($"[SYSTEM] {key} Toggled | UI On: {isOn} | Sending isDisabled: {isDisabled}");

                    if (this.DataContext is SystemViewModel vm)
                    {
                        vm.UpdateCounters();
                        vm.ApplyRecommendations();
                    }

                    await Task.Run(async () =>
                    {
                        if (_sysTweaks != null)
                        {
                            await _sysTweaks.ApplyTweaks(key, isDisabled);
                        }
                    });

                    if (NotificationManager.SysActions.TryGetValue(key, out var action))
                    {
                        NotificationManager.Show().WithDuration(300).Perform(action);
                    }
                }
            }
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (sender is Slider slider && slider.IsLoaded)
            {
                if (Math.Abs(e.NewValue - e.OldValue) > 0.1)
                {
                    _sysTweaks?.ApplyTweaksSlider(slider.Name, (uint)slider.Value);
                }
            }
        }

        #endregion

        #region Power Mode Management
        private async Task InitializePowerModeAsync()
        {
            _isInitializingPowerMode = true;
            try
            {
                var powerPlans = await GetAvailablePowerPlansAsync();
                var activePlanGuid = await GetActivePowerPlanGuidAsync();

                PowerModeComboBox.Items.Clear();

                foreach (var (guid, name) in powerPlans)
                {
                    var item = new ComboBoxItem
                    {
                        Content = name,
                        Tag = guid
                    };
                    PowerModeComboBox.Items.Add(item);

                    if (!string.IsNullOrEmpty(activePlanGuid) && guid.Equals(activePlanGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        PowerModeComboBox.SelectedItem = item;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            finally
            {
                _isInitializingPowerMode = false;
            }
        }

        private async Task<List<(string Guid, string Name)>> GetAvailablePowerPlansAsync()
        {
            var powerPlans = new List<(string Guid, string Name)>();
            try
            {
                var output = await CommandExecutor.StartTaskAsync("powercfg /list");

                var matches = Regex.Matches(output, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\s+\(([^)]+)\)");
                foreach (Match match in matches)
                {
                    var guid = match.Groups[1].Value.ToLowerInvariant();
                    var name = match.Groups[2].Value.Trim();
                    powerPlans.Add((guid, name));
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            return powerPlans;
        }

        private async Task<string?> GetActivePowerPlanGuidAsync()
        {
            try
            {
                var output = await CommandExecutor.StartTaskAsync("powercfg /getactivescheme");

                var match = Regex.Match(output, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");
                if (match.Success)
                {
                    return match.Groups[1].Value.ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
            return null;
        }

        private async Task SetPowerPlanAsync(string guid)
        {
            try
            {
                await CommandExecutor.StartTaskAsync($"powercfg /setactive {guid}");
                Debug.WriteLine($"Power plan set to: {guid}");
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
            }
        }

        private async void PowerModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingPowerMode)
                return;

            if (PowerModeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string guid)
            {
                await SetPowerPlanAsync(guid);
            }
        }

        private async void AddUltimatePowerPlanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddUltimatePowerPlanButton.IsEnabled = false;

                var powerPlans = await GetAvailablePowerPlansAsync();
                var ultimateExists = powerPlans.Any(p =>
                    p.Guid.Equals("e9a42b02-d5df-448d-aa00-03f14749eb61", StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains("Ultimate", StringComparison.OrdinalIgnoreCase));

                var title = ResourceString.GetString("AddUltimatePowerPlanTitle");

                if (ultimateExists)
                {
                    App.ShowNotification(
                        title,
                        ResourceString.GetString("UltimatePowerPlanExists"),
                        InfoBarSeverity.Success,
                        3000);
                }
                else
                {
                    await CommandExecutor.StartTaskAsync("powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61");
                    Debug.WriteLine("Added Ultimate Performance power plan");

                    await InitializePowerModeAsync();

                    App.ShowNotification(
                        title,
                        ResourceString.GetString("UltimatePowerPlanAdded"),
                        InfoBarSeverity.Success,
                        3000);
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                App.ShowNotification(
                    ResourceString.GetString("AddUltimatePowerPlanTitle"),
                    ResourceString.GetString("UnexpectedError"),
                    InfoBarSeverity.Error,
                    3000);
            }
            finally
            {
                AddUltimatePowerPlanButton.IsEnabled = true;
            }
        }

        private async void CreatePowerPlanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var nameTextBox = new TextBox
                {
                    PlaceholderText = ResourceString.GetString("PowerPlanNamePlaceholder"),
                    MaxLength = 50,
                    Margin = new Thickness(0, 8, 0, 16)
                };

                var powerPlans = await GetAvailablePowerPlansAsync();
                var basePlanComboBox = new ComboBox
                {
                    MinWidth = 250,
                    Margin = new Thickness(0, 8, 0, 0)
                };

                foreach (var (guid, name) in powerPlans)
                {
                    basePlanComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = name,
                        Tag = guid
                    });
                }

                if (basePlanComboBox.Items.Count > 0)
                {
                    basePlanComboBox.SelectedIndex = 0;
                }

                var contentPanel = new StackPanel
                {
                    Children =
                {
                    new TextBlock
                    {
                        Text = ResourceString.GetString("PowerPlanNameLabel"),
                        Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
                    },
                    nameTextBox,
                    new TextBlock
                    {
                        Text = ResourceString.GetString("BasePowerPlanLabel"),
                        Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
                    },
                    basePlanComboBox
                }
                };

                var createDialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                    BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
                    Title = ResourceString.GetString("CreatePowerPlanTitle"),
                    Content = contentPanel,
                    PrimaryButtonText = ResourceString.GetString("Create"),
                    CloseButtonText = ResourceString.GetString("Cancel"),
                    PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                    DefaultButton = ContentDialogButton.Primary
                };

                var result = await createDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    var planName = nameTextBox.Text.Trim();

                    if (string.IsNullOrWhiteSpace(planName))
                    {
                        App.ShowNotification(
                            ResourceString.GetString("CreatePowerPlanTitle"),
                            ResourceString.GetString("PowerPlanNameRequired"),
                            InfoBarSeverity.Warning,
                            3000);
                        return;
                    }

                    if (basePlanComboBox.SelectedItem is not ComboBoxItem selectedItem || selectedItem.Tag is not string baseGuid)
                    {
                        App.ShowNotification(
                            ResourceString.GetString("CreatePowerPlanTitle"),
                            ResourceString.GetString("BasePowerPlanRequired"),
                            InfoBarSeverity.Warning,
                            3000);
                        return;
                    }

                    var createOutput = await CommandExecutor.StartTaskAsync($"powercfg /duplicatescheme {baseGuid}");

                    var match = Regex.Match(createOutput,
                        @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");

                    if (match.Success)
                    {
                        var newGuid = match.Groups[1].Value;

                        await CommandExecutor.StartTaskAsync($"powercfg /changename {newGuid} \"{planName}\"");

                        Debug.WriteLine($"Created new power plan '{planName}' with GUID: {newGuid}");

                        await InitializePowerModeAsync();

                        App.ShowNotification(
                            ResourceString.GetString("CreatePowerPlanTitle"),
                            ResourceString.GetString("PowerPlanCreated"),
                            InfoBarSeverity.Success,
                            3000);
                    }
                    else
                    {
                        ErrorLogging.LogDebug(new Exception($"Failed to parse new power plan GUID from output: {createOutput}"));
                        App.ShowNotification(
                            ResourceString.GetString("CreatePowerPlanTitle"),
                            ResourceString.GetString("UnexpectedError"),
                            InfoBarSeverity.Error,
                            3000);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);
                App.ShowNotification(
                    ResourceString.GetString("CreatePowerPlanTitle"),
                    ResourceString.GetString("UnexpectedError"),
                    InfoBarSeverity.Error,
                    3000);
            }
        }
        #endregion

        #region Windows Updates Management
        private async Task InitializeWindowsUpdatesAsync()
        {
            _isInitializingWindowsUpdates = true;
            try
            {
                string savedMode = await Task.Run(() =>
                {
                    using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                        Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                            ? RegistryView.Registry64
                            : RegistryView.Default).OpenSubKey(RegistryBaseKey);

                    return key?.GetValue("WindowsUpdatesMode") as string ?? "Default";
                });

                foreach (ComboBoxItem item in WindowsUpdatesComboBox.Items)
                {
                    if (item.Tag is string tag && tag.Equals(savedMode, StringComparison.OrdinalIgnoreCase))
                    {
                        WindowsUpdatesComboBox.SelectedItem = item;
                        break;
                    }
                }

                if (WindowsUpdatesComboBox.SelectedItem == null && WindowsUpdatesComboBox.Items.Count > 0)
                {
                    WindowsUpdatesComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogDebug(ex);

                if (WindowsUpdatesComboBox.Items.Count > 0)
                {
                    WindowsUpdatesComboBox.SelectedIndex = 0;
                }
            }
            finally
            {
                _isInitializingWindowsUpdates = false;
            }
        }

        private async void WindowsUpdatesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingWindowsUpdates)
                return;

            if (WindowsUpdatesComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string mode)
            {
                try
                {
                    using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                        Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                            ? RegistryView.Registry64
                            : RegistryView.Default).CreateSubKey(RegistryBaseKey);

                    key?.SetValue("WindowsUpdatesMode", mode, RegistryValueKind.String);

                    switch (mode)
                    {
                        case "Default":
                            await SystemTweaks.SetWindowsUpdatesDefault();
                            break;
                        case "Security":
                            await SystemTweaks.SetWindowsUpdatesSecurityOnly();
                            break;
                        case "Manually":
                            await SystemTweaks.SetWindowsUpdatesManually();
                            break;
                        case "Disabled":
                            await SystemTweaks.SetWindowsUpdatesDisabled();
                            break;
                    }

                    Debug.WriteLine($"Automatic updates mode set to: {mode}");
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug(ex);
                }
            }
        }
        #endregion

        #region OS Compression
        private async void CompressOSButton_Click(object sender, RoutedEventArgs e)
        {
            var status = await CommandExecutor.StartTask("compact.exe /compactos:query");

            var compressDialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                BorderBrush = (SolidColorBrush)Application.Current.Resources["AccentAAFillColorDefaultBrush"],
                PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                SecondaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
                Title = ResourceString.GetString("SystemCompressionTitle"),
                Content = status,
                PrimaryButtonText = ResourceString.GetString("Compress"),
                SecondaryButtonText = ResourceString.GetString("Decompress"),
                CloseButtonText = ResourceString.GetString("Cancel")
            };

            compressDialog.PrimaryButtonClick += async (sender, args) =>
            {
                CompressOSButton.Visibility = Visibility.Collapsed;
                CompressOSProgressRing.Visibility = Visibility.Visible;
                CompressOSProgressText.Text = ResourceString.GetString("Compressing");
                var result = await CommandExecutor.StartTask("compact.exe /compactos:always");
                App.ShowNotification(ResourceString.GetString("SystemCompressionTitle"), result, InfoBarSeverity.Success, 5000);
                CompressOSButton.Visibility = Visibility.Visible;
                CompressOSProgressRing.Visibility = Visibility.Collapsed;
                CompressOSProgressText.Text = string.Empty;
            };

            compressDialog.SecondaryButtonClick += async (sender, args) =>
            {
                CompressOSButton.Visibility = Visibility.Collapsed;
                CompressOSProgressRing.Visibility = Visibility.Visible;
                CompressOSProgressText.Text = ResourceString.GetString("Decompressing");
                var result = await CommandExecutor.StartTask("compact.exe /compactos:never");
                App.ShowNotification(ResourceString.GetString("SystemCompressionTitle"), result, InfoBarSeverity.Success, 5000);
                CompressOSButton.Visibility = Visibility.Visible;
                CompressOSProgressRing.Visibility = Visibility.Collapsed;
                CompressOSProgressText.Text = string.Empty;
            };
            await compressDialog.ShowAsync();
        }
        #endregion

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {

        }

        #region Diagnostics & Debugging
        private void DebugAvailableCards()
        {
            var allButtonKeys = Enumerable.Range(1, 32).Select(i => $"TglButton{i}").ToList();

            var existingCards = UIHelper.FindVisualChildren<Border>(this)
                                .Where(c => c.Tag?.ToString()?.StartsWith("TglButton") == true)
                                .ToList();

            Debug.WriteLine("--- SYSTEM PAGE DIAGNOSTICS ---");

            foreach (var key in allButtonKeys)
            {
                var card = existingCards.FirstOrDefault(c => string.Equals(c.Tag?.ToString(), key, StringComparison.Ordinal));

                if (card == null)
                {
                    Debug.WriteLine($"[MISSING] {key}: Card is not in the XAML at all.");
                }
                else if (card.Visibility == Visibility.Collapsed)
                {
                    Debug.WriteLine($"[HIDDEN] {key}: Card exists but is hidden by Win11/Build logic.");
                }
                else
                {
                    Debug.WriteLine($"[OK] {key}: Visible.");
                }
            }

            int visibleCount = existingCards.Count(c => c.Visibility == Visibility.Visible);
            Debug.WriteLine($"Total Cards Visible: {visibleCount}");
            Debug.WriteLine("----------------------------------");
        }
        #endregion

        #region Purge Page
        public Task Purge()
        {
            Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and ViewModel...");

                this.Loaded -= SystemPage_Loaded;
                this.Unloaded -= SystemPage_Unloaded;

                this.DataContext = null;
                this.Content = null;
                //this.Bindings?.StopTracking();

                _ = Task.Run(() =>
                {
                    DiagnosticsPageViewModel.Current?.ForceImmediateMemoryCleanup();
                });
            }
            else
            {
                Debug.WriteLine($"[{this.GetType().Name}] High Performance Mode: State preserved in RAM cache.");
            }

            return Task.CompletedTask;
        }
        #endregion
    }
}