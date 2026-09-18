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

        private bool _isTaskbarLeft;
        public bool IsTaskbarLeft { get => _isTaskbarLeft; set => SetProperty(ref _isTaskbarLeft, value); }

        private bool _isTaskbarTop;
        public bool IsTaskbarTop { get => _isTaskbarTop; set => SetProperty(ref _isTaskbarTop, value); }

        private bool _isTaskbarRight;
        public bool IsTaskbarRight { get => _isTaskbarRight; set => SetProperty(ref _isTaskbarRight, value); }

        private bool _isTaskbarBottom;
        public bool IsTaskbarBottom { get => _isTaskbarBottom; set => SetProperty(ref _isTaskbarBottom, value); }
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
            StartMenuToggle.IsOn = SettingsEngine.Shell_StartMenuEnabled;
            StartMenuAnimationsToggle.IsOn = SettingsEngine.Shell_StartMenuAnimation;
            TaskbarToggle.IsOn = SettingsEngine.Shell_TaskbarEnabled;
            PreviewButtonsToggle.IsOn = SettingsEngine.Shell_TaskbarPreviewButtons;
            PreviewAnimationsToggle.IsOn = SettingsEngine.Shell_TaskbarPreviewAnimation;
            ClockSecondsToggle.IsOn = SettingsEngine.Shell_TaskbarClockSeconds;
            ShowUnpinnedToggle.IsOn = SettingsEngine.Shell_TaskbarShowUnpinned;
            HoverBackgroundToggle.IsOn = SettingsEngine.Shell_TaskbarHoverBackground;
            MonitorAwareToggle.IsOn = SettingsEngine.Shell_TaskbarMonitorAware;

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
            PreviewSpeedSlider.Value = SettingsEngine.Shell_TaskbarPreviewAnimSpeed;
            StartMenuSpeedSlider.Value = SettingsEngine.Shell_StartMenuAnimSpeed;

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
            StartMenuToggle.IsEnabled = isMasterEnabled;
            TaskbarToggle.IsEnabled = isMasterEnabled;

            AppFontCombo.IsEnabled = isMasterEnabled;
            AppFontSizeCombo.IsEnabled = isMasterEnabled;

            StartMenuStyleCombo.IsEnabled = isMasterEnabled && StartMenuToggle.IsOn;
            StartMenuAnimationsToggle.IsEnabled = isMasterEnabled && StartMenuToggle.IsOn;
            StartMenuAnimStyleCombo.IsEnabled = isMasterEnabled && StartMenuToggle.IsOn && StartMenuAnimationsToggle.IsOn;

            TaskbarSizeSlider.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            TaskbarIconSizeSlider.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            StartMenuSpeedSlider.IsEnabled = isMasterEnabled && StartMenuToggle.IsOn && StartMenuAnimationsToggle.IsOn;

            TaskbarStyleCombo.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            TaskbarAlignmentCombo.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
            TaskbarPositionPanel.IsEnabled = isMasterEnabled && TaskbarToggle.IsOn;
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
                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_AnimSpeed:{SettingsEngine.Shell_StartMenuAnimSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Enable:{TaskbarToggle.IsOn}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Style:{SettingsEngine.Shell_TaskbarStyle}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Alignment:{SettingsEngine.Shell_TaskbarAlignment}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Position:{SettingsEngine.Shell_TaskbarPosition ?? "Bottom"}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_PreviewButtons:{SettingsEngine.Shell_TaskbarPreviewButtons}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_PreviewAnimation:{SettingsEngine.Shell_TaskbarPreviewAnimation}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_PreviewAnimStyle:{SettingsEngine.Shell_TaskbarPreviewAnimStyle ?? "Standard"}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_PreviewAnimSpeed:{SettingsEngine.Shell_TaskbarPreviewAnimSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_ClockSeconds:{SettingsEngine.Shell_TaskbarClockSeconds}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_ShowUnpinned:{SettingsEngine.Shell_TaskbarShowUnpinned}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_UnpinnedMode:{SettingsEngine.Shell_TaskbarUnpinnedMode}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Animation:{SettingsEngine.Shell_TaskbarAnimation ?? "Spring"}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_HoverAnimation:{SettingsEngine.Shell_TaskbarHoverAnimation ?? "Standard"}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_HoverBackground:{SettingsEngine.Shell_TaskbarHoverBackground}");
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_MonitorAware:{SettingsEngine.Shell_TaskbarMonitorAware}");
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

        private void TaskbarSizeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int size = (int)e.NewValue;
            SettingsEngine.Shell_TaskbarSize = size;

            if (MasterToggle.IsOn && TaskbarToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_Size:{size}");
            }
        }

        private void TaskbarIconSizeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            int size = (int)e.NewValue;
            SettingsEngine.Shell_TaskbarIconSize = size;

            if (MasterToggle.IsOn && TaskbarToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_IconSize:{size}");
            }
        }

        private void PreviewSpeedSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            SettingsEngine.Shell_TaskbarPreviewAnimSpeed = e.NewValue;

            if (MasterToggle.IsOn && TaskbarToggle.IsOn && PreviewAnimationsToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"Taskbar_PreviewAnimSpeed:{e.NewValue.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        private void StartMenuSpeedSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;
            SettingsEngine.Shell_StartMenuAnimSpeed = e.NewValue;

            if (MasterToggle.IsOn && StartMenuToggle.IsOn && StartMenuAnimationsToggle.IsOn)
            {
                _ = ShellEnhancerController.SendCommandAsync($"StartMenu_AnimSpeed:{e.NewValue.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }
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