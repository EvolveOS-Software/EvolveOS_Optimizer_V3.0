// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class ShellCustomizationPage : Page, INotifyPropertyChanged
    {
        private bool _isInitialized = false;

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return;
            storage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Bound Properties
        private string _currentWallpaper = string.Empty;
        public string CurrentWallpaper
        {
            get => _currentWallpaper;
            set => SetProperty(ref _currentWallpaper, value);
        }

        public ObservableCollection<MonitorPositionViewModel> Monitors { get; } = new();

        public double GetOnOpacity(bool isOn) => isOn ? 1.0 : 0.0;
        public double GetOffOpacity(bool isOn) => isOn ? 0.0 : 1.0;

        private bool _isTaskbarLeft;
        public bool IsTaskbarLeft { get => _isTaskbarLeft; set => SetProperty(ref _isTaskbarLeft, value); }

        private bool _isTaskbarTop;
        public bool IsTaskbarTop { get => _isTaskbarTop; set => SetProperty(ref _isTaskbarTop, value); }

        private bool _isTaskbarRight;
        public bool IsTaskbarRight { get => _isTaskbarRight; set => SetProperty(ref _isTaskbarRight, value); }

        private bool _isTaskbarBottom;
        public bool IsTaskbarBottom { get => _isTaskbarBottom; set => SetProperty(ref _isTaskbarBottom, value); }

        private DateTime _lastLengthUpdate = DateTime.MinValue;
        private int _lengthDebounceToken = 0;

        private DateTime _lastRadiusUpdate = DateTime.MinValue;
        private int _radiusDebounceToken = 0;
        #endregion

        public ShellCustomizationPage()
        {
            this.InitializeComponent();
            LoadCurrentWallpaper();
            LoadSavedSettings();
            _isInitialized = true;
        }

        private void LoadCurrentWallpaper()
        {
            string path = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg";
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                if (key?.GetValue("Wallpaper") is string wallpaperPath)
                {
                    if (File.Exists(wallpaperPath))
                    {
                        path = wallpaperPath;
                    }
                }
            }
            catch { }

            CurrentWallpaper = new Uri(path).AbsoluteUri;
        }

        private async void LoadSavedSettings()
        {
            MasterToggle.IsOn = SettingsEngine.Shell_MasterEnabled;
            ShellStartupToggle.IsOn = SettingsEngine.Shell_RunOnStartup;
            ShellHighPriorityToggle.IsOn = SettingsEngine.Shell_HighPriority;
            StartMenuToggle.IsOn = SettingsEngine.Shell_StartMenuEnabled;
            StartMenuAnimationsToggle.IsOn = SettingsEngine.Shell_StartMenuAnimation;
            ProfileClickToggle.IsOn = SettingsEngine.Shell_StartMenuProfileClick;
            RecentDocsToggle.IsOn = SettingsEngine.Shell_StartMenuRecentDocs;

            PowerSleepToggle.IsOn = SettingsEngine.Shell_StartMenuPowerSleep;
            PowerLogOffToggle.IsOn = SettingsEngine.Shell_StartMenuPowerLogOff;
            PowerRestartBiosToggle.IsOn = SettingsEngine.Shell_StartMenuPowerRestartBios;

            TaskbarToggle.IsOn = SettingsEngine.Shell_TaskbarEnabled;
            PreviewButtonsToggle.IsOn = SettingsEngine.Shell_TaskbarPreviewButtons;
            PreviewAnimationsToggle.IsOn = SettingsEngine.Shell_TaskbarPreviewAnimation;
            ClockSecondsToggle.IsOn = SettingsEngine.Shell_TaskbarClockSeconds;
            ShowUnpinnedToggle.IsOn = SettingsEngine.Shell_TaskbarShowUnpinned;
            HoverBackgroundToggle.IsOn = SettingsEngine.Shell_TaskbarHoverBackground;
            MonitorAwareToggle.IsOn = SettingsEngine.Shell_TaskbarMonitorAware;

            SelectComboBoxItemByTag(ShellLanguageCombo, SettingsEngine.Shell_Language);
            SelectComboBoxItemByTag(StartMenuStyleCombo, SettingsEngine.Shell_StartMenuStyle);
            SelectComboBoxItemByTag(StartMenuAnimStyleCombo, SettingsEngine.Shell_StartMenuAnimStyle ?? "Standard");
            SelectComboBoxItemByTag(TaskbarStyleCombo, SettingsEngine.Shell_TaskbarStyle);
            SelectComboBoxItemByTag(TaskbarAlignmentCombo, SettingsEngine.Shell_TaskbarAlignment ?? "Center");
            SelectComboBoxItemByTag(UnpinnedModeCombo, SettingsEngine.Shell_TaskbarUnpinnedMode);
            SelectComboBoxItemByTag(TaskbarAnimationCombo, SettingsEngine.Shell_TaskbarAnimation ?? "Spring");
            SelectComboBoxItemByTag(TaskbarHoverAnimationCombo, SettingsEngine.Shell_TaskbarHoverAnimation ?? "Standard");
            SelectComboBoxItemByTag(AppFontCombo, SettingsEngine.Shell_AppFont ?? "Segoe UI");
            SelectComboBoxItemByTag(AppFontSizeCombo, SettingsEngine.Shell_AppFontSize.ToString());
            SelectComboBoxItemByTag(PreviewAnimStyleCombo, SettingsEngine.Shell_TaskbarPreviewAnimStyle ?? "Standard");

            TaskbarSizeSlider.Value = SettingsEngine.Shell_TaskbarSize;
            TaskbarIconSizeSlider.Value = SettingsEngine.Shell_TaskbarIconSize;
            TaskbarLengthSlider.Value = SettingsEngine.Shell_TaskbarLength;
            TaskbarCornerRadiusSlider.Value = SettingsEngine.Shell_TaskbarCornerRadius;
            PreviewSpeedSlider.Value = SettingsEngine.Shell_TaskbarPreviewAnimSpeed;
            StartMenuSpeedSlider.Value = SettingsEngine.Shell_StartMenuAnimSpeed;
            PreviewDelaySlider.Value = SettingsEngine.Shell_TaskbarPreviewDelay;

            string savedPos = SettingsEngine.Shell_TaskbarPosition ?? "Bottom";
            var posDict = new System.Collections.Generic.Dictionary<string, string>();

            if (savedPos.Contains(":"))
            {
                foreach (var part in savedPos.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = part.Split(':');
                    if (kv.Length == 2) posDict[kv[0]] = kv[1];
                }
            }

            var displays = DisplayArea.FindAll();

            IDesktopWallpaper? wallpaperManager = null;
            try { wallpaperManager = (IDesktopWallpaper)new DesktopWallpaperClass(); } catch { }

            Monitors.Clear();
            for (int i = 0; i < displays.Count; i++)
            {
                var display = displays[i];
                string deviceId = display.DisplayId.Value.ToString();
                string name = display.IsPrimary ? "Primary Monitor" : $"Monitor {i + 1}";

                string pos = posDict.ContainsKey(deviceId) ? posDict[deviceId] : (savedPos.Contains(":") ? "Bottom" : savedPos);

                string wpPath = CurrentWallpaper;
                if (wallpaperManager != null)
                {
                    try
                    {
                        string monitorPath = wallpaperManager.GetMonitorDevicePathAt((uint)i);
                        wpPath = wallpaperManager.GetWallpaper(monitorPath);
                    }
                    catch { }
                }

                Monitors.Add(new MonitorPositionViewModel
                {
                    DisplayName = name,
                    DeviceId = deviceId,
                    WallpaperPath = string.IsNullOrEmpty(wpPath) ? CurrentWallpaper : wpPath,
                    Position = pos
                });
            }

            if (displays.Count > 1)
            {
                MonitorAwareContainer.Visibility = Visibility.Visible;
            }
            else
            {
                MonitorAwareContainer.Visibility = Visibility.Collapsed;
            }

            UpdateChildControlStates(MasterToggle.IsOn);

            if (MasterToggle.IsOn && Process.GetProcessesByName("EvolveOS_ShellEnhancer").Length == 0)
            {
                await ShellEnhancerController.StartEnhancerAsync();
            }
        }

        private void SelectComboBoxItemByTag(ComboBox comboBox, string tag)
        {
            var item = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(x => x.Tag?.ToString() == tag);
            if (item != null)
            {
                comboBox.SelectedItem = item;
            }
            else if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private void UpdateChildControlStates(bool isMasterEnabled)
        {
            ShellStartupToggle.IsEnabled = isMasterEnabled;
            ShellHighPriorityToggle.IsEnabled = isMasterEnabled;
            ShellLanguageCombo.IsEnabled = isMasterEnabled;
            StartMenuToggle.IsEnabled = isMasterEnabled;
            TaskbarToggle.IsEnabled = isMasterEnabled;

            AppFontCombo.IsEnabled = isMasterEnabled;
            AppFontSizeCombo.IsEnabled = isMasterEnabled;

            StartMenuStyleCombo.IsEnabled = isMasterEnabled && StartMenuToggle.IsOn;
            StartMenuAnimationsToggle.IsEnabled = isMasterEnabled && StartMenuToggle.IsOn;
            StartMenuAnimStyleCombo.IsEnabled = isMasterEnabled && StartMenuToggle.IsOn && StartMenuAnimationsToggle.IsOn;
            ProfileClickToggle.IsEnabled = isMasterEnabled && StartMenuToggle.IsOn;
            RecentDocsToggle.IsEnabled = isMasterEnabled && StartMenuToggle.IsOn;

            PowerSleepToggle.IsEnabled = isMasterEnabled && StartMenuToggle.IsOn;
            PowerLogOffToggle.IsEnabled = isMasterEnabled && StartMenuToggle.IsOn;
            PowerRestartBiosToggle.IsEnabled = isMasterEnabled && StartMenuToggle.IsOn;

            TaskbarSizeSlider.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            TaskbarIconSizeSlider.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            TaskbarLengthSlider.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            TaskbarCornerRadiusSlider.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            StartMenuSpeedSlider.IsEnabled = isMasterEnabled && StartMenuToggle.IsOn && StartMenuAnimationsToggle.IsOn;
            PreviewDelaySlider.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;

            TaskbarStyleCombo.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            TaskbarAlignmentCombo.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            TaskbarPositionPanel.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            BtnTaskbarPins.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            PreviewButtonsToggle.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            PreviewAnimationsToggle.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            PreviewAnimStyleCombo.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn && PreviewAnimationsToggle.IsOn;
            PreviewSpeedSlider.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn && PreviewAnimationsToggle.IsOn;
            ClockSecondsToggle.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            ShowUnpinnedToggle.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            UnpinnedModeCombo.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn && ShowUnpinnedToggle.IsOn;
            TaskbarAnimationCombo.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            TaskbarHoverAnimationCombo.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            HoverBackgroundToggle.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            MonitorAwareToggle.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
        }

        #region Event Handlers
        private async void MasterToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            bool isEnabled = MasterToggle.IsOn;
            SettingsEngine.Shell_MasterEnabled = isEnabled;

            UpdateChildControlStates(isEnabled);

            if (isEnabled)
            {
                await ShellEnhancerController.StartEnhancerAsync();

                _ = ShellEnhancerController.SendCommandAsync($"Shell_Font:{SettingsEngine.Shell_AppFont}");
                _ = ShellEnhancerController.SendCommandAsync($"Shell_FontSize:{SettingsEngine.Shell_AppFontSize}");

                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_Enable:{StartMenuToggle.IsOn}");
                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_Style:{SettingsEngine.Shell_StartMenuStyle}");
                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_Animation:{SettingsEngine.Shell_StartMenuAnimation}");
                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_AnimStyle:{SettingsEngine.Shell_StartMenuAnimStyle ?? "Standard"}");
                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_AnimSpeed:{SettingsEngine.Shell_StartMenuAnimSpeed.ToString(CultureInfo.InvariantCulture)}");
                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_ProfileClick:{SettingsEngine.Shell_StartMenuProfileClick}");
                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_RecentDocs:{SettingsEngine.Shell_StartMenuRecentDocs}");

                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_PowerSleep:{SettingsEngine.Shell_StartMenuPowerSleep}");
                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_PowerLogOff:{SettingsEngine.Shell_StartMenuPowerLogOff}");
                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_PowerRestartBios:{SettingsEngine.Shell_StartMenuPowerRestartBios}");

                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Enable:{TaskbarToggle.IsOn}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Size:{SettingsEngine.Shell_TaskbarSize}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_IconSize:{SettingsEngine.Shell_TaskbarIconSize}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Length:{SettingsEngine.Shell_TaskbarLength}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_CornerRadius:{SettingsEngine.Shell_TaskbarCornerRadius}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Style:{SettingsEngine.Shell_TaskbarStyle}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Alignment:{SettingsEngine.Shell_TaskbarAlignment}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Position:{SettingsEngine.Shell_TaskbarPosition ?? "Bottom"}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_PreviewButtons:{SettingsEngine.Shell_TaskbarPreviewButtons}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_PreviewAnimation:{SettingsEngine.Shell_TaskbarPreviewAnimation}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_PreviewAnimStyle:{SettingsEngine.Shell_TaskbarPreviewAnimStyle ?? "Standard"}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_PreviewAnimSpeed:{SettingsEngine.Shell_TaskbarPreviewAnimSpeed.ToString(CultureInfo.InvariantCulture)}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_PreviewDelay:{SettingsEngine.Shell_TaskbarPreviewDelay.ToString(CultureInfo.InvariantCulture)}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_ClockSeconds:{SettingsEngine.Shell_TaskbarClockSeconds}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_ShowUnpinned:{SettingsEngine.Shell_TaskbarShowUnpinned}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_UnpinnedMode:{SettingsEngine.Shell_TaskbarUnpinnedMode}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Animation:{SettingsEngine.Shell_TaskbarAnimation ?? "Spring"}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_HoverAnimation:{SettingsEngine.Shell_TaskbarHoverAnimation ?? "Standard"}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_HoverBackground:{SettingsEngine.Shell_TaskbarHoverBackground}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_MonitorAware:{SettingsEngine.Shell_TaskbarMonitorAware}");

                _ = ShellEnhancerController.SendCommandAsync($"Shell_HighPriority:{SettingsEngine.Shell_HighPriority}");
                _ = ShellEnhancerController.SendCommandAsync($"Shell_Language:{SettingsEngine.Shell_Language}");
            }
            else
            {
                _ = ShellEnhancerController.SendCommandAsync("StartMenu_Enable:False");
                _ = ShellEnhancerController.SendCommandAsync("Taskbar_Enable:False");

                await Task.Delay(300);

                ShellEnhancerController.StopEnhancer();
            }
        }

        private void SettingToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || sender is not ToggleSwitch toggle) return;

            string commandTag = toggle.Tag?.ToString() ?? string.Empty;

            if (commandTag == "StartMenu_Enable")
                SettingsEngine.Shell_StartMenuEnabled = toggle.IsOn;
            else if (commandTag == "StartMenu_Animation")
            {
                SettingsEngine.Shell_StartMenuAnimation = toggle.IsOn;
                StartMenuAnimStyleCombo.IsEnabled = MasterToggle.IsOn && StartMenuToggle.IsOn && toggle.IsOn;
                StartMenuSpeedSlider.IsEnabled = MasterToggle.IsOn && StartMenuToggle.IsOn && toggle.IsOn;
            }
            else if (commandTag == "StartMenu_ProfileClick")
                SettingsEngine.Shell_StartMenuProfileClick = toggle.IsOn;
            else if (commandTag == "StartMenu_PowerSleep")
                SettingsEngine.Shell_StartMenuPowerSleep = toggle.IsOn;
            else if (commandTag == "StartMenu_PowerLogOff")
                SettingsEngine.Shell_StartMenuPowerLogOff = toggle.IsOn;
            else if (commandTag == "StartMenu_PowerRestartBios")
                SettingsEngine.Shell_StartMenuPowerRestartBios = toggle.IsOn;
            else if (commandTag == "StartMenu_RecentDocs")
                SettingsEngine.Shell_StartMenuRecentDocs = toggle.IsOn;
            else if (commandTag == "Shell_RunOnStartup")
                SettingsEngine.Shell_RunOnStartup = toggle.IsOn;
            else if (commandTag == "Shell_HighPriority")
                SettingsEngine.Shell_HighPriority = toggle.IsOn;
            else if (commandTag == "Taskbar_Enable")
                SettingsEngine.Shell_TaskbarEnabled = toggle.IsOn;
            else if (commandTag == "Taskbar_PreviewButtons")
                SettingsEngine.Shell_TaskbarPreviewButtons = toggle.IsOn;
            else if (commandTag == "Taskbar_PreviewAnimation")
            {
                SettingsEngine.Shell_TaskbarPreviewAnimation = toggle.IsOn;
                PreviewAnimStyleCombo.IsEnabled = MasterToggle.IsOn && TaskbarToggle.IsOn && toggle.IsOn;
                PreviewSpeedSlider.IsEnabled = MasterToggle.IsOn && TaskbarToggle.IsOn && toggle.IsOn;
            }
            else if (commandTag == "Taskbar_ClockSeconds")
                SettingsEngine.Shell_TaskbarClockSeconds = toggle.IsOn;
            else if (commandTag == "Taskbar_HoverBackground")
                SettingsEngine.Shell_TaskbarHoverBackground = toggle.IsOn;
            else if (commandTag == "Taskbar_MonitorAware")
                SettingsEngine.Shell_TaskbarMonitorAware = toggle.IsOn;
            else if (commandTag == "Taskbar_ShowUnpinned")
            {
                SettingsEngine.Shell_TaskbarShowUnpinned = toggle.IsOn;
                UnpinnedModeCombo.IsEnabled = MasterToggle.IsOn && TaskbarToggle.IsOn && toggle.IsOn;
            }

            UpdateChildControlStates(MasterToggle.IsOn);

            if (MasterToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"{commandTag}:{toggle.IsOn}");
            }
        }

        private void StartMenuStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || StartMenuStyleCombo.SelectedItem is not ComboBoxItem selectedItem) return;

            string style = selectedItem.Tag?.ToString() ?? "Standard";
            SettingsEngine.Shell_StartMenuStyle = style;

            if (MasterToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_Style:{style}");
            }
        }

        private void SettingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || sender is not ComboBox comboBox || comboBox.SelectedItem is not ComboBoxItem selectedItem) return;

            string commandTag = comboBox.Tag?.ToString() ?? string.Empty;
            string style = selectedItem.Tag?.ToString() ?? "Standard";

            if (commandTag == "Taskbar_Style")
                SettingsEngine.Shell_TaskbarStyle = style;
            else if (commandTag == "Taskbar_Alignment")
                SettingsEngine.Shell_TaskbarAlignment = style;
            else if (commandTag == "Taskbar_UnpinnedMode")
                SettingsEngine.Shell_TaskbarUnpinnedMode = style;
            else if (commandTag == "Taskbar_Animation")
                SettingsEngine.Shell_TaskbarAnimation = style;
            else if (commandTag == "Taskbar_HoverAnimation")
                SettingsEngine.Shell_TaskbarHoverAnimation = style;
            else if (commandTag == "Shell_Font")
                SettingsEngine.Shell_AppFont = style;
            else if (commandTag == "Taskbar_PreviewAnimStyle")
                SettingsEngine.Shell_TaskbarPreviewAnimStyle = style;
            else if (commandTag == "StartMenu_AnimStyle")
                SettingsEngine.Shell_StartMenuAnimStyle = style;
            else if (commandTag == "Shell_FontSize")
            {
                if (double.TryParse(style, out double size))
                    SettingsEngine.Shell_AppFontSize = size;
            }

            if (MasterToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"{commandTag}:{style}");
            }
        }

        private void ShellLanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || sender is not ComboBox cb || cb.SelectedItem is not ComboBoxItem item) return;

            string langCode = item.Tag?.ToString() ?? "en-us";

            SettingsEngine.Shell_Language = langCode;

            if (MasterToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"Shell_Language:{langCode}");
            }
        }

        private void BtnTaskbarPins_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            try
            {
                Debug.WriteLine("[Navigation] Attempting to open TaskbarPinsPage...");

                if (this.Frame != null)
                {
                    this.Frame.Navigate(typeof(Pages.TaskbarPinsPage));

                    Debug.WriteLine("[Navigation] Navigate command sent successfully!");
                }
                else
                {
                    Debug.WriteLine("[Navigation] ERROR: this.Frame is null!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Navigation] CRASH PREVENTED: {ex.Message}");
                Debug.WriteLine($"[Navigation] STACK TRACE: {ex.StackTrace}");
            }
        }

        private void TaskbarSizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int size = (int)e.NewValue;
            if (TaskbarSizeValueText != null) TaskbarSizeValueText.Text = $"{size}px";
            SettingsEngine.Shell_TaskbarSize = size;

            if (MasterToggle.IsOn && TaskbarToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Size:{size}");
            }
        }

        private void TaskbarIconSizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int size = (int)e.NewValue;
            if (TaskbarIconSizeValueText != null) TaskbarIconSizeValueText.Text = $"{size}px";
            SettingsEngine.Shell_TaskbarIconSize = size;

            if (MasterToggle.IsOn && TaskbarToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_IconSize:{size}");
            }
        }

        private async void TaskbarLengthSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int length = (int)e.NewValue;
            if (TaskbarLengthValueText != null) TaskbarLengthValueText.Text = $"{length}%";
            SettingsEngine.Shell_TaskbarLength = length;

            if (MasterToggle.IsOn && TaskbarToggle.IsOn)
            {
                int currentToken = ++_lengthDebounceToken;

                if ((DateTime.Now - _lastLengthUpdate).TotalMilliseconds > 40)
                {
                    _lastLengthUpdate = DateTime.Now;
                    _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Length:{length}");
                }

                await Task.Delay(50);
                if (currentToken == _lengthDebounceToken)
                {
                    _lastLengthUpdate = DateTime.Now;
                    _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Length:{length}");
                }
            }
        }

        private async void TaskbarCornerRadiusSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int radius = (int)e.NewValue;
            if (TaskbarCornerRadiusValueText != null) TaskbarCornerRadiusValueText.Text = $"{radius}px";
            SettingsEngine.Shell_TaskbarCornerRadius = radius;

            if (MasterToggle.IsOn && TaskbarToggle.IsOn)
            {
                int currentToken = ++_radiusDebounceToken;

                if ((DateTime.Now - _lastRadiusUpdate).TotalMilliseconds > 40)
                {
                    _lastRadiusUpdate = DateTime.Now;
                    _ = ShellEnhancerController.SendCommandAsync($"Taskbar_CornerRadius:{radius}");
                }

                await Task.Delay(50);
                if (currentToken == _radiusDebounceToken)
                {
                    _lastRadiusUpdate = DateTime.Now;
                    _ = ShellEnhancerController.SendCommandAsync($"Taskbar_CornerRadius:{radius}");
                }
            }
        }

        private void PreviewSpeedSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            double speed = e.NewValue;
            if (PreviewSpeedValueText != null) PreviewSpeedValueText.Text = $"{speed:0.0}x";
            SettingsEngine.Shell_TaskbarPreviewAnimSpeed = speed;

            if (MasterToggle.IsOn && TaskbarToggle.IsOn && PreviewAnimationsToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_PreviewAnimSpeed:{speed.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        private void PreviewDelaySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            double delay = e.NewValue;
            if (PreviewDelayValueText != null) PreviewDelayValueText.Text = $"{delay:0.0}s";
            SettingsEngine.Shell_TaskbarPreviewDelay = delay;

            if (MasterToggle.IsOn && TaskbarToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_PreviewDelay:{delay.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        private void StartMenuSpeedSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            double speed = e.NewValue;
            if (StartMenuSpeedValueText != null) StartMenuSpeedValueText.Text = $"{speed:0.0}x";
            SettingsEngine.Shell_StartMenuAnimSpeed = speed;

            if (MasterToggle.IsOn && StartMenuToggle.IsOn && StartMenuAnimationsToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_AnimSpeed:{speed.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        private void ResetTaskbarSize_Click(object sender, RoutedEventArgs e)
        {
            if (TaskbarSizeSlider != null) TaskbarSizeSlider.Value = 48;
        }

        private void ResetTaskbarIconSize_Click(object sender, RoutedEventArgs e)
        {
            if (TaskbarIconSizeSlider != null) TaskbarIconSizeSlider.Value = 24;
        }

        private void ResetTaskbarLength_Click(object sender, RoutedEventArgs e)
        {
            if (TaskbarLengthSlider != null) TaskbarLengthSlider.Value = 100;
        }

        private void ResetTaskbarCornerRadius_Click(object sender, RoutedEventArgs e)
        {
            if (TaskbarCornerRadiusSlider != null) TaskbarCornerRadiusSlider.Value = 8;
        }

        private void ResetPreviewSpeed_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewSpeedSlider != null) PreviewSpeedSlider.Value = 1.0;
        }

        private void ResetPreviewDelay_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewDelaySlider != null) PreviewDelaySlider.Value = 0.3;
        }

        private void ResetStartMenuSpeed_Click(object sender, RoutedEventArgs e)
        {
            if (StartMenuSpeedSlider != null) StartMenuSpeedSlider.Value = 1.0;
        }

        private void TaskbarPos_Click(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || sender is not RadioButton rb) return;

            if (rb.DataContext is MonitorPositionViewModel monitor)
            {
                monitor.Position = rb.Tag?.ToString() ?? "Bottom";

                var positions = Enumerable.Select(Monitors, m => $"{m.DeviceId}:{m.Position}");
                string newSetting = string.Join(";", positions);

                SettingsEngine.Shell_TaskbarPosition = newSetting;

                if (MasterToggle.IsOn && TaskbarToggle.IsOn)
                {
                    _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Position:{newSetting}");
                }
            }
        }
        #endregion
    }
}