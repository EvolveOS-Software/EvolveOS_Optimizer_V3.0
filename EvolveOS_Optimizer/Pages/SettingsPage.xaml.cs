// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text.Json;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Animation;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Managers;
using EvolveOS_Optimizer.Utilities.Services;
using EvolveOS_Optimizer.Utilities.Tweaks;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SettingsPage : Page, INotifyPropertyChanged, IPurgeable
    {
        #region Fields
        private bool _isInitialized;
        private string _pendingHexColor = "#FF0078D4";

        private PropertyChangedEventHandler? _localizationHandler;

        private DispatcherTimer? _sessionTimer;
        private DateTime _sessionExpiryTime;

        private DispatcherTimer? _themeSchedulerTimer;
        private string _lastScheduledTheme = string.Empty;
        #endregion

        #region Events & Properties
        public event PropertyChangedEventHandler? PropertyChanged;
        public LocalizationService Localizer => LocalizationService.Instance;

        public bool IsUpdateCheckRequired
        {
            get => SettingsEngine.IsUpdateCheckRequired;
            set => SettingsEngine.IsUpdateCheckRequired = value;
        }

        private bool _isAdmin;
        public bool IsAdmin
        {
            get => _isAdmin;
            set
            {
                if (_isAdmin != value)
                {
                    _isAdmin = value;
                    OnPropertyChanged();
                }
            }
        }
        #endregion

        #region Localization Helper
        public string GetText(string key) => Localizer[key];
        #endregion

        #region Constructor
        public SettingsPage()
        {
            InitializeComponent();

            _localizationHandler = (s, e) =>
            {
                if (e.PropertyName == "Item[]")
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(100);

                        if (this.XamlRoot != null)
                        {
                            OnPropertyChanged(string.Empty);
                            UpdateComboBoxLocalization();
                        }
                    });
                }
            };

            LocalizationService.Instance.PropertyChanged += _localizationHandler;

            InitializeSelections();
            UpdateComboBoxLocalization();
            SetSelectedByTag(ThemeSelector, SettingsEngine.AppTheme);

            this.Loaded += SettingsPage_Loaded;
            this.Unloaded += SettingsPage_Unloaded;
        }
        #endregion

        #region Page Lifecycle
        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            TintOpacitySlider.Value = SettingsEngine.AcrylicOpacity;
            LuminositySlider.Value = SettingsEngine.AcrylicLuminosity;

            var savedColor = UIHelper.ToColor(SettingsEngine.AcrylicTintColor);
            AcrylicColorPicker.Color = savedColor;
            ColorPreview.Background = new SolidColorBrush(savedColor);

            PopulateTranslationHotkeyComboBoxes();

            _isInitialized = true;

            ApplyUIPermissions();

            string currentBackdrop = SettingsEngine.Backdrop;
            foreach (ComboBoxItem item in BackdropSelector.Items)
            {
                if (item.Tag?.ToString() == currentBackdrop)
                {
                    BackdropSelector.SelectedItem = item;
                    break;
                }
            }

            if (currentBackdrop == "AcrylicThin")
            {
                AcrylicOptionsPanel.Visibility = Visibility.Visible;
                AcrylicOptionsPanel.Opacity = 1.0;
                PanelTransform.Y = 0;
            }

            if (SliderSessionHours != null)
            {
                SliderSessionHours.Value = SettingsEngine.AutoLoginSessionHours;
            }

            if (AuthSessionManager.IsSessionValid(out string? sessionUser, out DateTime expiry))
            {
                _isInitialized = false;

                if (CbEnableAutoLogin != null) CbEnableAutoLogin.IsOn = true;
                if (AutoLoginSettings != null) AutoLoginSettings.Visibility = Visibility.Visible;

                StartLiveSessionTimer(expiry);

                _isInitialized = true;
            }

            if (BtnDeveloperMode != null)
            {
                BtnDeveloperMode.IsOn = LocalMachineSettingsEngine.IsDeveloperMode;
            }

            InitializeAutoThemeScheduler();
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Purge();
        }
        #endregion

        #region Initialization Helpers
        private void ApplyUIPermissions()
        {
            IsAdmin = string.Equals(UserSession.UserType, "Admin", StringComparison.OrdinalIgnoreCase);

            SecurityPrivacy.Visibility = IsAdmin ? Visibility.Visible : Visibility.Collapsed;

            Debug.WriteLine($"[Settings] Permissions Applied. UserType: {UserSession.UserType}, IsAdmin: {IsAdmin}");
        }

        private void InitializeSelections()
        {
            SetSelectedByTag(LanguageSelector, SettingsEngine.Language);
            SetSelectedByTag(BackdropSelector, SettingsEngine.Backdrop);

            var savedColor = SettingsEngine.AccentColor;
            ColorPalette.SelectedItem = ColorPalette.Items
                .FirstOrDefault(i => i is Rectangle r && r.Tag?.ToString() == savedColor);

            try
            {
                var hex = savedColor.Replace("#", string.Empty);
                if (hex.Length == 6) hex = "FF" + hex;
                var a = (byte)uint.Parse(hex[..2], NumberStyles.HexNumber);
                var r = (byte)uint.Parse(hex[2..4], NumberStyles.HexNumber);
                var g = (byte)uint.Parse(hex[4..6], NumberStyles.HexNumber);
                var b = (byte)uint.Parse(hex[6..8], NumberStyles.HexNumber);

                AdvancedColorPicker.Color = Microsoft.UI.ColorHelper.FromArgb(a, r, g, b);
            }
            catch
            {
                AdvancedColorPicker.Color = Microsoft.UI.ColorHelper.FromArgb(255, 0, 120, 212);
            }
        }

        private void UpdateComboBoxLocalization()
        {
            foreach (var item in BackdropSelector.Items.Cast<ComboBoxItem>())
            {
                var tag = item.Tag?.ToString() ?? "";
                item.Content = Localizer[$"Settings_Backdrop_{tag}"];
            }
            foreach (var item in ThemeSelector.Items.Cast<ComboBoxItem>())
            {
                var tag = item.Tag?.ToString() ?? "";
                item.Content = Localizer[$"Settings_Theme_{tag}"];
            }
        }

        private void SetSelectedByTag(ComboBox comboBox, string tag) =>
            comboBox.SelectedItem = comboBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Tag?.ToString() == tag);
        #endregion

        #region Event Handlers - Selection Changes
        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized && LanguageSelector.SelectedItem is ComboBoxItem item)
            {
                SettingsEngine.Language = item.Tag?.ToString() ?? "en-us";

                if (App.MainWindow is EvolveOS_Optimizer.MainWindow mainWindow)
                {
                    mainWindow.RefreshTrayIconLanguage();
                }
            }
        }

        private void BackdropSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || BackdropSelector.SelectedItem is not ComboBoxItem item) return;

            string selected = item.Tag?.ToString() ?? "None";
            SettingsEngine.Backdrop = selected;

            bool showOptions = (selected == "AcrylicThin");

            if (showOptions)
            {
                AcrylicOptionsPanel.Visibility = Visibility.Visible;
                ShowPanelAnimation.Begin();
            }
            else
            {
                AcrylicOptionsPanel.Visibility = Visibility.Collapsed;
                AcrylicOptionsPanel.Opacity = 0;
                PanelTransform.Y = -20;
            }

            if (App.MainWindow is Window mainWindow)
            {
                UIHelper.ApplyBackdrop(mainWindow, selected);
            }
        }
        #endregion

        #region Event Handlers - Sliders & Colors
        private void AcrylicSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (sender is Slider slider)
            {
                if (slider == TintOpacitySlider)
                    SettingsEngine.AcrylicOpacity = e.NewValue;
                else if (slider == LuminositySlider)
                    SettingsEngine.AcrylicLuminosity = e.NewValue;

                if (App.MainWindow is Window window)
                {
                    UIHelper.ApplyBackdrop(window, "AcrylicThin");
                }
            }
        }

        private void AcrylicColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (!_isInitialized) return;

            string hex = args.NewColor.ToString();
            SettingsEngine.AcrylicTintColor = hex;

            ColorPreview.Background = new SolidColorBrush(args.NewColor);

            if (App.MainWindow is Window mainWindow)
            {
                UIHelper.ApplyBackdrop(mainWindow, SettingsEngine.Backdrop);
            }
        }

        private void ResetAcrylicColor_Click(object sender, RoutedEventArgs e)
        {
            AcrylicColorPicker.Color = Colors.Black;
        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized && ThemeSelector.SelectedItem is ComboBoxItem item)
            {
                string selectedTheme = item.Tag?.ToString() ?? "Default";
                SettingsEngine.AppTheme = selectedTheme;
                SettingsEngine.UpdateTheme(selectedTheme);
            }
        }

        private void ColorPalette_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized && ColorPalette.SelectedItem is Rectangle rect && rect.Tag != null)
            {
                string newColor = rect.Tag.ToString()!;
                SettingsEngine.AccentColor = newColor;

                ((App)Application.Current).UpdateGlobalAccentColor(newColor);
            }
        }

        private void AdvancedColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args) =>
            _pendingHexColor = $"#{args.NewColor.A:X2}{args.NewColor.R:X2}{args.NewColor.G:X2}{args.NewColor.B:X2}";

        private void ApplyCustomColor_Click(object sender, RoutedEventArgs e)
        {
            ColorPalette.SelectedItem = null;
            SettingsEngine.AccentColor = _pendingHexColor;

            ((App)Application.Current).UpdateGlobalAccentColor(_pendingHexColor);
        }
        #endregion

        #region Auto Theme / LightSwitch Feature

        public bool IsAutoThemeEnabled
        {
            get => SettingsEngine.IsAutoThemeEnabled;
            set
            {
                if (SettingsEngine.IsAutoThemeEnabled != value)
                {
                    SettingsEngine.IsAutoThemeEnabled = value;

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsManualThemeEnabled));

                    InitializeAutoThemeScheduler();
                }
            }
        }

        public bool IsManualThemeEnabled => !IsAutoThemeEnabled;

        public TimeSpan LightThemeTime
        {
            get => SettingsEngine.LightThemeTime;
            set
            {
                if (SettingsEngine.LightThemeTime != value)
                {
                    SettingsEngine.LightThemeTime = value;
                    OnPropertyChanged();
                    CheckAutoThemeState();
                }
            }
        }

        public bool SyncOsThemeWithApp
        {
            get => SettingsEngine.SyncOsThemeWithApp;
            set
            {
                if (SettingsEngine.SyncOsThemeWithApp != value)
                {
                    SettingsEngine.SyncOsThemeWithApp = value;
                    OnPropertyChanged();
                }
            }
        }

        public TimeSpan DarkThemeTime
        {
            get => SettingsEngine.DarkThemeTime;
            set
            {
                if (SettingsEngine.DarkThemeTime != value)
                {
                    SettingsEngine.DarkThemeTime = value;
                    OnPropertyChanged();
                    CheckAutoThemeState();
                }
            }
        }

        private void InitializeAutoThemeScheduler()
        {
            if (IsAutoThemeEnabled)
            {
                if (_themeSchedulerTimer == null)
                {
                    _themeSchedulerTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
                    _themeSchedulerTimer.Tick += (s, e) => CheckAutoThemeState();
                }
                _themeSchedulerTimer.Start();
                CheckAutoThemeState();
            }
            else
            {
                _themeSchedulerTimer?.Stop();
            }
        }

        private void CheckAutoThemeState()
        {
            if (!IsAutoThemeEnabled) return;

            var currentTime = DateTime.Now.TimeOfDay;
            string targetTheme = "Default";

            if (LightThemeTime < DarkThemeTime)
            {
                targetTheme = (currentTime >= LightThemeTime && currentTime < DarkThemeTime) ? "Light" : "Dark";
            }
            else
            {
                targetTheme = (currentTime >= LightThemeTime || currentTime < DarkThemeTime) ? "Light" : "Dark";
            }

            if (SettingsEngine.AppTheme != targetTheme && _lastScheduledTheme != targetTheme)
            {
                _lastScheduledTheme = targetTheme;
                SettingsEngine.AppTheme = targetTheme;

                _isInitialized = false;
                SetSelectedByTag(ThemeSelector, targetTheme);
                _isInitialized = true;

                SettingsEngine.UpdateTheme(targetTheme);

                if (SettingsEngine.SyncOsThemeWithApp)
                {
                    SettingsEngine.SetWindowsSystemTheme(targetTheme);
                }

                if (App.MainWindow is Window mainWindow)
                {
                    UIHelper.ApplyBackdrop(mainWindow, SettingsEngine.Backdrop);
                }
            }
        }

        #endregion

        #region Event Handlers - Updates & Toggles
        private async void ManualUpdateCheck_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            btn.IsEnabled = false;
            btn.Content = ResourceString.GetString("Settings_Update_Checking");

            bool updateFound = SystemDiagnostics.IsNeedUpdate;

            if (updateFound)
            {
                if (App.MainWindow is EvolveOS_Optimizer.MainWindow mainWin)
                {
                    mainWin.AnimateUpdateBanner(true);
                }
            }
            else
            {
                btn.Content = ResourceString.GetString("Settings_Update_UpToDate");
                await Task.Delay(2000);
            }

            btn.IsEnabled = true;
            btn.Content = ResourceString.GetString("Settings_Update_CheckButton");
        }

        private void BtnStartMinimized_ChangedState(object sender, RoutedEventArgs e)
        {
            if (sender is Microsoft.UI.Xaml.Controls.ToggleSwitch toggleSwitch)
            {
                SettingsEngine.IsStartMinimized = toggleSwitch.IsOn;
            }
        }

        private void BtnRunOnStartUp_ChangedState(object sender, RoutedEventArgs e)
        {
            if (sender is Microsoft.UI.Xaml.Controls.ToggleSwitch toggleSwitch)
            {
                SettingsEngine.IsRunOnStartUp = toggleSwitch.IsOn;
            }
        }

        private void MergeStrings_Click(object sender, RoutedEventArgs e)
        {
            string currentLang = SettingsEngine.Language;

            if (currentLang.Equals("en-us", StringComparison.OrdinalIgnoreCase))
            {
                NativeToastHelper.SendNativeToast("Developer Tools", "Cannot merge missing strings into the base English (en-us) dictionary.");
                return;
            }

            ResourceHelper.MergeMissingStringsToXaml(currentLang);

            LocalizationService.Instance.LoadLanguage(currentLang);

            NativeToastHelper.SendNativeToast("Developer Tools", $"Successfully merged missing strings for {currentLang}.");
        }
        #endregion

        #region Developer Tools
        private void BtnDeveloperMode_ChangedState(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            if (sender is ToggleSwitch ts)
            {
                LocalMachineSettingsEngine.IsDeveloperMode = ts.IsOn;

                if (!ts.IsOn)
                {
                    IsTranslationHotkeyEnabled = false;
                }

                Loc.RefreshAll();
            }
        }

        public bool IsTranslationHotkeyEnabled
        {
            get => LocalMachineSettingsEngine.IsTranslationHotkeyEnabled;
            set
            {
                if (LocalMachineSettingsEngine.IsTranslationHotkeyEnabled != value)
                {
                    LocalMachineSettingsEngine.IsTranslationHotkeyEnabled = value;
                    OnPropertyChanged();
                    App.NotifyHotkeySettingsChanged();
                }
            }
        }

        private async void OpenMissingStringsJson_Click(object sender, RoutedEventArgs e)
        {
            string langCode = SettingsEngine.Language;

            string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
            string realBaseDir = System.IO.Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

            string jsonPath = System.IO.Path.Combine(realBaseDir, "Languages", $"MissingStrings_{langCode}.json");

            if (File.Exists(jsonPath))
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(jsonPath);

                    var options = new Windows.System.LauncherOptions
                    {
                        DisplayApplicationPicker = true
                    };

                    await Windows.System.Launcher.LaunchFileAsync(file, options);
                }
                catch (Exception ex)
                {
                    NativeToastHelper.SendNativeToast("Developer Tools", $"Failed to open file: {ex.Message}");
                }
            }
            else
            {
                NativeToastHelper.SendNativeToast("Developer Tools", $"No missing strings logged for {langCode} yet.");
            }
        }

        private void PopulateTranslationHotkeyComboBoxes()
        {
            _isInitialized = false;

            CbTranslationModifier.Items.Clear();
            CbTranslationModifier.Items.Add(new ComboBoxItem { Content = "Ctrl", Tag = Windows.System.VirtualKeyModifiers.Control });
            CbTranslationModifier.Items.Add(new ComboBoxItem { Content = "Alt", Tag = Windows.System.VirtualKeyModifiers.Menu });
            CbTranslationModifier.Items.Add(new ComboBoxItem { Content = "Shift", Tag = Windows.System.VirtualKeyModifiers.Shift });
            CbTranslationModifier.Items.Add(new ComboBoxItem { Content = "Ctrl + Shift", Tag = Windows.System.VirtualKeyModifiers.Control | Windows.System.VirtualKeyModifiers.Shift });
            CbTranslationModifier.Items.Add(new ComboBoxItem { Content = "Ctrl + Alt", Tag = Windows.System.VirtualKeyModifiers.Control | Windows.System.VirtualKeyModifiers.Menu });

            CbTranslationKey.Items.Clear();
            for (int i = 65; i <= 90; i++)
            {
                var key = (Windows.System.VirtualKey)i;
                CbTranslationKey.Items.Add(new ComboBoxItem { Content = key.ToString(), Tag = key });
            }

            var savedMod = (Windows.System.VirtualKeyModifiers)LocalMachineSettingsEngine.TranslationHotkeyModifier;
            CbTranslationModifier.SelectedItem = CbTranslationModifier.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (Windows.System.VirtualKeyModifiers)i.Tag == savedMod) ?? CbTranslationModifier.Items[3];

            var savedKey = (Windows.System.VirtualKey)LocalMachineSettingsEngine.TranslationHotkeyKey;
            CbTranslationKey.SelectedItem = CbTranslationKey.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (Windows.System.VirtualKey)i.Tag == savedKey) ?? CbTranslationKey.Items[11]; // Defaults to 'L'

            _isInitialized = true;
        }

        private void CbTranslationModifier_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || CbTranslationModifier.SelectedItem is not ComboBoxItem item) return;
            LocalMachineSettingsEngine.TranslationHotkeyModifier = (int)item.Tag;
            App.NotifyHotkeySettingsChanged();
        }

        private void CbTranslationKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || CbTranslationKey.SelectedItem is not ComboBoxItem item) return;
            LocalMachineSettingsEngine.TranslationHotkeyKey = (int)item.Tag;
            App.NotifyHotkeySettingsChanged();
        }

        private async void CreateNewLanguage_Click(object sender, RoutedEventArgs e)
        {
            string newLangCode = TxtNewLangCode.Text.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(newLangCode) || newLangCode.Length < 2)
            {
                NativeToastHelper.SendNativeToast("Developer Tools", "Please enter a valid language code (e.g., es-es or pt).");
                return;
            }

            string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
            string realBaseDir = System.IO.Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

            string langDir = System.IO.Path.Combine(realBaseDir, "Languages");
            string englishPath = System.IO.Path.Combine(langDir, "en-us.xaml");
            string newLangPath = System.IO.Path.Combine(langDir, $"{newLangCode}.xaml");

            if (File.Exists(newLangPath))
            {
                NativeToastHelper.SendNativeToast("Developer Tools", $"The language file {newLangCode}.xaml already exists!");
                return;
            }

            if (!File.Exists(englishPath))
            {
                NativeToastHelper.SendNativeToast("Developer Tools", "Error: The base en-us.xaml file could not be found.");
                return;
            }

            try
            {
                File.Copy(englishPath, newLangPath);

                NativeToastHelper.SendNativeToast("Developer Tools", $"Successfully created {newLangCode}.xaml! Choose an editor to open it.");
                TxtNewLangCode.Text = string.Empty;

                var file = await StorageFile.GetFileFromPathAsync(newLangPath);

                var options = new Windows.System.LauncherOptions
                {
                    DisplayApplicationPicker = true
                };

                await Windows.System.Launcher.LaunchFileAsync(file, options);
            }
            catch (Exception ex)
            {
                NativeToastHelper.SendNativeToast("Developer Tools", $"Failed to create language file: {ex.Message}");
            }
        }

        private void LocateLanguageFile_Click(object sender, RoutedEventArgs e)
        {
            string langCode = SettingsEngine.Language;

            string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
            string realBaseDir = System.IO.Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

            string langPath = System.IO.Path.Combine(realBaseDir, "Languages", $"{langCode}.xaml");

            ShowInExplorer(langPath);
        }

        private void ShowInExplorer(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {

                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                catch (Exception ex)
                {
                    NativeToastHelper.SendNativeToast("Developer Tools", $"Failed to open Explorer: {ex.Message}");
                }
            }
            else
            {
                NativeToastHelper.SendNativeToast("Developer Tools", "File does not exist yet.");
            }
        }
        #endregion

        #region Bound Properties
        public bool IsWindowBorderEnabled
        {
            get => SettingsEngine.IsWindowBorderEnabled;
            set
            {
                if (SettingsEngine.IsWindowBorderEnabled != value)
                {
                    if (MainWindow.Instance?.RootGrid?.DataContext is Core.ViewModel.MainWinViewModel mainVm)
                    {
                        mainVm.IsWindowBorderEnabled = value;
                    }
                    else
                    {
                        SettingsEngine.IsWindowBorderEnabled = value;
                    }
                }
            }
        }

        public bool IsRunOnStartUp
        {
            get => SettingsEngine.IsRunOnStartUp;
            set
            {
                if (SettingsEngine.IsRunOnStartUp != value)
                {
                    SettingsEngine.IsRunOnStartUp = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsCloseToTray
        {
            get => SettingsEngine.IsCloseToTrayEnabled;
            set
            {
                if (SettingsEngine.IsCloseToTrayEnabled != value)
                {
                    SettingsEngine.IsCloseToTrayEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsStartMinimized
        {
            get => SettingsEngine.IsStartMinimized;
            set
            {
                if (SettingsEngine.IsStartMinimized != value)
                {
                    SettingsEngine.IsStartMinimized = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool PerformDbBackup
        {
            get => SettingsEngine.PerformDbBackup;
            set
            {
                if (SettingsEngine.PerformDbBackup != value)
                {
                    SettingsEngine.PerformDbBackup = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EncryptDbBackupCopies
        {
            get => SettingsEngine.EncryptDbBackupCopies;
            set
            {
                if (SettingsEngine.EncryptDbBackupCopies != value)
                {
                    SettingsEngine.EncryptDbBackupCopies = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DatabaseBackupPath
        {
            get => SettingsEngine.DatabaseBackupPath;
            set
            {
                if (SettingsEngine.DatabaseBackupPath != value)
                {
                    SettingsEngine.DatabaseBackupPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool KeepBackupEnabled
        {
            get => SettingsEngine.KeepBackupEnabled;
            set
            {
                if (SettingsEngine.KeepBackupEnabled != value)
                {
                    SettingsEngine.KeepBackupEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowRestorePointOnStart
        {
            get => LocalMachineSettingsEngine.IsFirstRun;
            set
            {
                if (LocalMachineSettingsEngine.IsFirstRun != value)
                {
                    LocalMachineSettingsEngine.IsFirstRun = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EnableStartupMonitor
        {
            get => LocalMachineSettingsEngine.EnableStartupMonitor;
            set
            {
                if (LocalMachineSettingsEngine.EnableStartupMonitor != value)
                {
                    LocalMachineSettingsEngine.EnableStartupMonitor = value;
                    OnPropertyChanged();
                }
            }
        }
        #endregion

        #region INotifyPropertyChanged Implementation
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion

        #region Database Backup & Folder Logic
        private async void BrowseBackupFolder_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            folderPicker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                DatabaseBackupPath = folder.Path;
            }
        }

        private async void RestoreDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = App.MainWindow;
                if (window == null) return;

                var windowId = window.AppWindow.Id;

                var picker = new FileOpenPicker(windowId);

                picker.FileTypeFilter.Add(".bak");
                picker.FileTypeFilter.Add(".dat");

                var pickResult = await picker.PickSingleFileAsync();

                if (pickResult != null && !string.IsNullOrEmpty(pickResult.Path))
                {
                    bool success = SqlConnectionHelper.RestoreDatabase(pickResult.Path);

                    if (success)
                    {
                        ContentDialog successDialog = new ContentDialog
                        {
                            Title = ResourceString.GetString("Settings_DbRestore_Success_Title"),
                            Content = ResourceString.GetString("Settings_DbRestore_Success_Content"),
                            CloseButtonText = ResourceString.GetString("Settings_DbRestore_Success_Button"),
                            XamlRoot = this.XamlRoot
                        };

                        await successDialog.ShowAsync();

                        SettingsEngine.SelfReboot();
                    }
                    else
                    {
                        ContentDialog errorDialog = new ContentDialog
                        {
                            Title = ResourceString.GetString("Settings_DbRestore_Error_Title"),
                            Content = ResourceString.GetString("Settings_DbRestore_Error_Content"),
                            CloseButtonText = ResourceString.GetString("Settings_DbRestore_Error_Button"),
                            XamlRoot = this.XamlRoot
                        };

                        await errorDialog.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsPage] File picker error: {ex.Message}");
            }
        }
        #endregion

        #region System Recovery
        private async void BtnRevertToDefault_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            bool restorePointExists = await DoesEvolveOsRestorePointExistAsync();

            string dialogTitle = restorePointExists
                ? (ResourceString.GetString("Settings_RevertDialogTitle") ?? "Revert to Defaults?")
                : (ResourceString.GetString("Settings_RevertMissingTitle") ?? "Restore Point Missing");

            string dialogContent = restorePointExists
                ? (ResourceString.GetString("Settings_RevertDialogContent") ?? "EvolveOS Optimizer uses Windows System Restore to safely revert all changes made to your system.\n\nClicking 'Continue' will open the Windows System Restore wizard. Please select the restore point named 'EvolveOS Initial Backup' to undo all tweaks.")
                : (ResourceString.GetString("Settings_RevertMissingContent") ?? "The 'EvolveOS Initial Backup' restore point could not be found. You may have opted out of creating it, or Windows automatically deleted it to free up disk space.\n\nYou can still continue to open the System Restore wizard to look for older, manual restore points.");

            ContentDialog confirmDialog = new ContentDialog
            {
                Title = dialogTitle,
                Content = dialogContent,
                PrimaryButtonText = ResourceString.GetString("Generic_Continue") ?? "Continue",
                CloseButtonText = ResourceString.GetString("Generic_Cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "rstrui.exe",
                        UseShellExecute = true,
                        Verb = "runas"
                    });
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug($"Failed to launch rstrui.exe: {ex.Message}");
                    NativeToastHelper.SendNativeToast(ResourceString.GetString("toast_error_title") ?? "Error", ResourceString.GetString("toast_rstrui_error") ?? "Could not launch Windows System Restore automatically.");
                }
            }

            if (btn != null) btn.IsEnabled = true;
        }

        private async Task<bool> DoesEvolveOsRestorePointExistAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"root\default", "SELECT * FROM SystemRestore"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            string description = obj["Description"]?.ToString() ?? string.Empty;

                            if (description.Contains("EvolveOS", StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    ErrorLogging.LogDebug($"WMI Restore Point Query Failed: {ex.Message}");
                    return true;
                }
            });
        }

        private async void BtnRestoreOptimizerTweaks_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            ContentDialog confirmDialog = new ContentDialog
            {
                Title = ResourceString.GetString("Settings_RestoreTweaksDialogTitle") ?? "Restore Optimizer Tweaks?",
                Content = ResourceString.GetString("Settings_RestoreTweaksDialogContent") ?? "This will safely revert all settings modified by EvolveOS Optimizer back to the exact state they were in when you first launched the app.\n\nAre you sure you want to continue?",
                PrimaryButtonText = ResourceString.GetString("Generic_Continue") ?? "Continue",
                CloseButtonText = ResourceString.GetString("Generic_Cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    App.ShowNotification(
                        ResourceString.GetString("toast_restoring_title") ?? "Restoring",
                        ResourceString.GetString("toast_restore_snapshot_msg") ?? "Reverting optimizer tweaks to original state...",
                        InfoBarSeverity.Informational,
                        4000
                    );

                    await BackupManager.RestoreInitialSnapshotAsync();

                    new SystemTweaks().AnalyzeAndUpdate();
                    new InterfaceTweaks().AnalyzeAndUpdate();
                    new PrivacyTweaks().AnalyzeAndUpdate();
                    new ServicesTweaks().AnalyzeAndUpdate();

                    App.ShowNotification(
                        ResourceString.GetString("toast_success_title") ?? "Success",
                        ResourceString.GetString("toast_restore_snapshot_success") ?? "Optimizer tweaks have been reverted. Please restart your PC.",
                        InfoBarSeverity.Success,
                        4000
                    );
                }
                catch (Exception ex)
                {
                    NativeToastHelper.SendNativeToast(
                        ResourceString.GetString("toast_error_title") ?? "Error",
                        (ResourceString.GetString("toast_restore_error") ?? "Failed to restore tweaks: ") + ex.Message
                    );
                }
            }

            if (btn != null) btn.IsEnabled = true;
        }

        private async void BtnForceWindowsDefaults_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            ContentDialog confirmDialog = new ContentDialog
            {
                Title = ResourceString.GetString("Settings_ForceWindowsDefaultsDialogTitle") ?? "Force Windows Defaults?",
                Content = ResourceString.GetString("Settings_ForceWindowsDefaultsDialogContent") ?? "This will wipe out all custom settings managed by the optimizer and force your system back to standard Microsoft factory defaults.\n\nThis is highly recommended if you used a custom ISO or debloat script before installing EvolveOS.\n\nAre you sure you want to continue?",
                PrimaryButtonText = ResourceString.GetString("Generic_Continue") ?? "Continue",
                CloseButtonText = ResourceString.GetString("Generic_Cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    App.ShowNotification(
                        ResourceString.GetString("toast_restoring_title") ?? "Restoring",
                        ResourceString.GetString("toast_force_defaults_msg") ?? "Forcing system settings back to Microsoft defaults...",
                        InfoBarSeverity.Informational,
                        4000
                    );

                    await BackupManager.RestoreToFactoryDefaultsAsync();

                    new SystemTweaks().AnalyzeAndUpdate();
                    new InterfaceTweaks().AnalyzeAndUpdate();
                    new PrivacyTweaks().AnalyzeAndUpdate();
                    new ServicesTweaks().AnalyzeAndUpdate();

                    App.ShowNotification(
                        ResourceString.GetString("toast_success_title") ?? "Success",
                        ResourceString.GetString("toast_force_defaults_success") ?? "Windows factory defaults restored. Please restart your PC.",
                        InfoBarSeverity.Success,
                        4000
                    );
                }
                catch (Exception ex)
                {
                    NativeToastHelper.SendNativeToast(
                        ResourceString.GetString("toast_error_title") ?? "Error",
                        (ResourceString.GetString("toast_force_defaults_error") ?? "Failed to force defaults: ") + ex.Message
                    );
                }
            }

            if (btn != null) btn.IsEnabled = true;
        }

        private async void BtnResetAppSettings_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            ContentDialog confirmDialog = new ContentDialog
            {
                Title = ResourceString.GetString("Settings_ResetAppDialogTitle") ?? "Reset App Preferences?",
                Content = ResourceString.GetString("Settings_ResetAppDialogContent") ?? "This will reset all EvolveOS Optimizer preferences (themes, dashboard layout, auto-optimization thresholds, etc.) back to their defaults.\n\nThis will NOT revert your Windows tweaks.\n\nContinue?",
                PrimaryButtonText = ResourceString.GetString("Generic_Continue") ?? "Continue",
                CloseButtonText = ResourceString.GetString("Generic_Cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    SettingsEngine.Reset();
                    LocalMachineSettingsEngine.Reset();

                    App.ShowNotification(
                        ResourceString.GetString("toast_success_title") ?? "Success",
                        ResourceString.GetString("toast_reset_app_success") ?? "Application preferences have been reset.",
                        InfoBarSeverity.Success,
                        4000
                    );
                }
                catch (Exception ex)
                {
                    NativeToastHelper.SendNativeToast(
                        ResourceString.GetString("toast_error_title") ?? "Error",
                        "Failed to reset app preferences: " + ex.Message
                    );
                }
            }

            if (btn != null) btn.IsEnabled = true;
        }

        private async void BtnExportAppSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

                var savePicker = new FileSavePicker(windowId)
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = "EvolveOS_Preferences_Backup"
                };
                savePicker.FileTypeChoices.Add("JSON File", new List<string>() { ".json" });

                var file = await savePicker.PickSaveFileAsync();

                if (file != null && !string.IsNullOrEmpty(file.Path))
                {
                    var backup = new AppSettingsBackup
                    {
                        CurrentUserSettings = SettingsEngine.ExportSettings(),
                        LocalMachineSettings = LocalMachineSettingsEngine.ExportSettings()
                    };

                    string json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });

                    await File.WriteAllTextAsync(file.Path, json);

                    App.ShowNotification(
                        ResourceString.GetString("toast_export_success_title") ?? "Success",
                        ResourceString.GetString("toast_export_app_msg") ?? "App preferences exported successfully.",
                        InfoBarSeverity.Success,
                        4000
                    );
                }
            }
            catch (Exception ex)
            {
                NativeToastHelper.SendNativeToast("Error", "Failed to export settings: " + ex.Message);
            }
        }

        private async void BtnImportAppSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

                var openPicker = new FileOpenPicker(windowId)
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                };
                openPicker.FileTypeFilter.Add(".json");

                var file = await openPicker.PickSingleFileAsync();

                if (file != null && !string.IsNullOrEmpty(file.Path))
                {
                    ContentDialog confirmDialog = new ContentDialog
                    {
                        Title = ResourceString.GetString("Settings_ImportAppDialogTitle") ?? "Import App Preferences?",
                        Content = ResourceString.GetString("Settings_ImportAppDialogContent") ?? "This will overwrite your current EvolveOS Optimizer preferences (themes, layout, behavior) with the settings from this file.\n\nAre you sure you want to continue?",
                        PrimaryButtonText = ResourceString.GetString("Generic_Continue") ?? "Continue",
                        CloseButtonText = ResourceString.GetString("Generic_Cancel") ?? "Cancel",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };

                    var result = await confirmDialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        string json = await File.ReadAllTextAsync(file.Path);
                        var backup = JsonSerializer.Deserialize<AppSettingsBackup>(json);

                        if (backup != null)
                        {
                            SettingsEngine.ImportSettings(backup.CurrentUserSettings);
                            LocalMachineSettingsEngine.ImportSettings(backup.LocalMachineSettings);

                            App.ShowNotification(
                                ResourceString.GetString("toast_import_success_title") ?? "Import Successful",
                                ResourceString.GetString("toast_import_app_success_msg") ?? "App preferences imported successfully.",
                                InfoBarSeverity.Success,
                                4000
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                NativeToastHelper.SendNativeToast(
                    ResourceString.GetString("toast_error_title") ?? "Error",
                    "Failed to import settings: " + ex.Message
                );
            }
        }
        #endregion

        #region Tweak Profiles Management

        private async void BtnExportTweakProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

                var savePicker = new FileSavePicker(windowId)
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = "EvolveOS_Tweak_Profile"
                };
                savePicker.FileTypeChoices.Add("JSON File", new List<string>() { ".json" });

                var file = await savePicker.PickSaveFileAsync();

                if (file != null && !string.IsNullOrEmpty(file.Path))
                {
                    var profile = await TweakProfileManager.GenerateExportProfileAsync();

                    string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(file.Path, json);

                    App.ShowNotification(
                        ResourceString.GetString("toast_export_success_title") ?? "Export Successful",
                        ResourceString.GetString("toast_profile_exported") ?? "Tweak profile saved successfully.",
                        InfoBarSeverity.Success,
                        4000
                    );
                }
            }
            catch (Exception ex)
            {
                App.ShowNotification(
                    ResourceString.GetString("toast_error_title") ?? "Error",
                    ResourceString.GetString("toast_profile_export_error") ?? "Failed to export profile: " + ex.Message,
                    InfoBarSeverity.Error,
                    5000
                );
            }
        }

        private async void BtnImportTweakProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);

                var openPicker = new FileOpenPicker(windowId)
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                };
                openPicker.FileTypeFilter.Add(".json");

                var file = await openPicker.PickSingleFileAsync();

                if (file != null && !string.IsNullOrEmpty(file.Path))
                {
                    ContentDialog confirmDialog = new ContentDialog
                    {
                        Title = ResourceString.GetString("Settings_ImportProfileDialogTitle") ?? "Apply Tweak Profile?",
                        Content = ResourceString.GetString("Settings_ImportProfileDialogContent") ?? "This will immediately apply all the Windows settings and optimizations saved in this profile.\n\nAre you sure you want to continue?",
                        PrimaryButtonText = ResourceString.GetString("Generic_Continue") ?? "Continue",
                        CloseButtonText = ResourceString.GetString("Generic_Cancel") ?? "Cancel",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };

                    var result = await confirmDialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        string json = await File.ReadAllTextAsync(file.Path);
                        var profile = JsonSerializer.Deserialize<TweakProfileBackup>(json);

                        if (profile != null)
                        {
                            await TweakProfileManager.ApplyImportedProfileAsync(profile);

                            App.ShowNotification(
                                ResourceString.GetString("toast_profile_import_title") ?? "Optimization Complete",
                                ResourceString.GetString("toast_profile_imported") ?? "Tweak profile imported and applied to Windows.",
                                InfoBarSeverity.Success,
                                4000
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.ShowNotification(
                    ResourceString.GetString("toast_error_title") ?? "Error",
                    ResourceString.GetString("toast_profile_import_error") ?? "Failed to import profile: " + ex.Message,
                    InfoBarSeverity.Error,
                    5000
                );
            }
        }

        #endregion

        #region Auto-Login Session Logic
        private async void BtnAutoLogin_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }

            if (this.XamlRoot == null) return;

            if (CbEnableAutoLogin != null && !CbEnableAutoLogin.IsOn)
            {
                if (AuthSessionManager.IsSessionValid(out _, out _))
                {
                    var confirmDialog = new ContentDialog
                    {
                        Title = ResourceString.GetString("title_end_session") ?? "End Session?",
                        Content = ResourceString.GetString("msg_end_session_confirm") ?? "An Auto-Login session is currently active. Are you sure you want to end it and remove your saved credentials?",
                        PrimaryButtonText = ResourceString.GetString("btn_yes_end_session") ?? "Yes, End Session",
                        CloseButtonText = ResourceString.GetString("btn_cancel") ?? "Cancel",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot
                    };

                    var result = await confirmDialog.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        _sessionTimer?.Stop();
                        AuthSessionManager.ClearSession();

                        if (AutoLoginSettings != null && LblSessionExpiry != null)
                        {
                            LblSessionExpiry.Text = ResourceString.GetString("lbl_session_expire_default") ?? "Session will expire at: --:--";
                            AutoLoginSettings.Visibility = Visibility.Collapsed;
                        }

                        NativeToastHelper.SendNativeToast(
                            ResourceString.GetString("toast_title_autologin") ?? "Auto-Login",
                            ResourceString.GetString("toast_msg_session_ended") ?? "Session ended and credentials removed."
                        );
                    }
                    else
                    {
                        _isInitialized = false;
                        CbEnableAutoLogin.IsOn = true;
                        _isInitialized = true;
                    }
                }
                else
                {
                    if (AutoLoginSettings != null) AutoLoginSettings.Visibility = Visibility.Collapsed;
                }

                return;
            }

            var passwordBox = new PasswordBox
            {
                PlaceholderText = ResourceString.GetString("txt_enter_password") ?? "Enter your password...",
                Width = 300,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var errorTextBlock = new TextBlock
            {
                Text = ResourceString.GetString("msg_invalid_password") ?? "Invalid password. Please try again.",
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 12
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = ResourceString.GetString("msg_verify_identity") ?? "Please verify your identity to enable Auto-Login.", TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(passwordBox);
            panel.Children.Add(errorTextBlock);

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("title_authorize_autologin") ?? "Authorize Auto-Login",
                Content = panel,
                PrimaryButtonText = ResourceString.GetString("btn_verify") ?? "Verify",
                CloseButtonText = ResourceString.GetString("btn_cancel") ?? "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            dialog.PrimaryButtonClick += async (s, args) =>
            {
                args.Cancel = true;

                string plainPassword = passwordBox.Password;
                passwordBox.IsEnabled = false;

                try
                {
                    await Task.Run(() =>
                    {
                        var psi = new ProcessStartInfo("sqllocaldb", "start MSSQLLocalDB")
                        {
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        Process.Start(psi)?.WaitForExit();
                    });

                    var userDataAccess = new UserDataAccess(SqlConnectionHelper.connectReturn());
                    var loginData = await userDataAccess.GetPasswordAndImageAsync(UserSession.Username!);

                    bool isVerified = loginData.PasswordHash != null && BCrypt.Net.BCrypt.Verify(plainPassword, loginData.PasswordHash);

                    passwordBox.IsEnabled = true;

                    if (isVerified)
                    {
                        using (SecureString machineKey = TokenManager.GetMachineKey())
                        {
                            string encryptedToken = AesHelper.Encrypt(plainPassword, machineKey);
                            TokenManager.SaveToken(UserSession.Username!, encryptedToken);
                        }

                        int hours = SettingsEngine.AutoLoginSessionHours;
                        AuthSessionManager.CreateAutoLoginSession(UserSession.Username!, hours);

                        UpdateExpiryLabel();
                        if (AutoLoginSettings != null) AutoLoginSettings.Visibility = Visibility.Visible;

                        NativeToastHelper.SendNativeToast(
                            ResourceString.GetString("toast_success") ?? "Success",
                            ResourceString.GetString("toast_autologin_authorized") ?? "Auto-Login Authorized!"
                        );

                        dialog.Hide();
                    }
                    else
                    {
                        FactoryAnimation.AnimateErrorShake(passwordBox);

                        errorTextBlock.Visibility = Visibility.Visible;
                        passwordBox.Password = string.Empty;
                        passwordBox.Focus(FocusState.Programmatic);
                    }
                }
                catch (Exception ex)
                {
                    passwordBox.IsEnabled = true;
                    NativeToastHelper.SendNativeToast(
                        ResourceString.GetString("toast_token_error") ?? "Database Error",
                        ex.Message
                    );
                    dialog.Hide();
                }
            };

            dialog.Closed += (s, args) =>
            {
                if (!AuthSessionManager.IsSessionValid(out _, out _))
                {
                    _isInitialized = false;
                    if (CbEnableAutoLogin != null) CbEnableAutoLogin.IsOn = false;
                    _isInitialized = true;
                }
            };

            await dialog.ShowAsync();
        }

        private void BtnAutoLogin_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }

            _sessionTimer?.Stop();

            AuthSessionManager.ClearSession();
            if (AutoLoginSettings != null && LblSessionExpiry != null)
            {
                LblSessionExpiry.Text = "Session will expire at: --:--";
                AutoLoginSettings.Visibility = Visibility.Collapsed;
            }
        }

        private void SliderSessionHours_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }
            SettingsEngine.AutoLoginSessionHours = (int)e.NewValue;
            UpdateExpiryLabel();
        }

        private void UpdateExpiryLabel()
        {
            if (LblSessionExpiry == null || SliderSessionHours == null)
            {
                return;
            }

            DateTime expiryTime = DateTime.Now.AddHours((int)SliderSessionHours.Value);
            string timeString = expiryTime.ToString("hh:mm tt");
            LblSessionExpiry.Text = $"Session will expire at: {timeString}";
        }
        #endregion

        #region Session Timer Management
        private void StartLiveSessionTimer(DateTime expiry)
        {
            _sessionExpiryTime = expiry;

            if (_sessionTimer == null)
            {
                _sessionTimer = new DispatcherTimer();
                _sessionTimer.Interval = TimeSpan.FromSeconds(1);
                _sessionTimer.Tick += (s, e) => UpdateLiveDisplay();
            }

            _sessionTimer.Start();
        }

        private async void UpdateLiveDisplay()
        {
            TimeSpan remaining = _sessionExpiryTime - DateTime.Now;
            double totalSeconds = remaining.TotalSeconds;

            if (totalSeconds <= 0)
            {
                _sessionTimer?.Stop();

                if (LblSessionExpiry != null)
                {
                    LblSessionExpiry.Text = ResourceString.GetString("lbl_session_expired") ?? "Session Expired";
                    LblSessionExpiry.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                }

                if (CbEnableAutoLogin != null) CbEnableAutoLogin.IsOn = false;

                if (LocalMachineSettingsEngine.IsDeveloperMode)
                {
                    string devMsg = ResourceString.GetString("msgbox_dev_mode_logout_prompt") ?? "Developer Mode is still active. Would you like to disable it before logging out?";
                    string devTitle = ResourceString.GetString("msgbox_dev_mode_active_title") ?? "Developer Mode Active";

                    var devResult = Win32Helper.MessageBox(IntPtr.Zero, devMsg, devTitle, Win32Helper.MB_YESNO);

                    if (devResult == Win32Helper.IDYES)
                    {
                        LocalMachineSettingsEngine.IsDeveloperMode = false;
                    }
                }

                UserSession.Clear();
                TokenManager.DeleteToken();

                string msg = ResourceString.GetString("msg_session_expired_login") ?? "Your session has expired. Please log in again.";

                DispatcherQueue.TryEnqueue(() =>
                {
                    var loginWin = new EvolveOS_Optimizer.Views.UserLoginWindow(new WeatherService(), msg);
                    if (App.MainWindow != null)
                    {
                        App.MainWindow.Close();
                    }
                    App.MainWindow = loginWin;
                    loginWin.Activate();
                });

                return;
            }

            if (LblSessionExpiry != null)
            {
                string prefix = ResourceString.GetString("lbl_session_expires_in") ?? "Expires in: ";
                LblSessionExpiry.Text = $"{prefix}{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            }
        }
        #endregion

        #region Purge Page
        public void Purge()
        {
            Debug.WriteLine("[SettingsPage] Purge initiated...");

            if (_sessionTimer != null)
            {
                _sessionTimer.Stop();
                _sessionTimer = null;
            }

            if (_themeSchedulerTimer != null)
            {
                _themeSchedulerTimer.Stop();
                _themeSchedulerTimer = null;
            }

            if (_localizationHandler != null)
            {
                LocalizationService.Instance.PropertyChanged -= _localizationHandler;
                _localizationHandler = null;
            }

            this.Loaded -= SettingsPage_Loaded;
            this.Unloaded -= SettingsPage_Unloaded;

            PropertyChanged = null;

            this.DataContext = null;
            this.Content = null;

            _isInitialized = false;

            Debug.WriteLine("[SettingsPage] Purge complete.");
        }
        #endregion
    }
}