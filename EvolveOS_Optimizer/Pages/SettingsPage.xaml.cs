using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Shapes;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SettingsPage : Page, INotifyPropertyChanged
    {
        private bool _isInitialized;
        private string _pendingHexColor = "#FF0078D4";

        public event PropertyChangedEventHandler? PropertyChanged;
        public LocalizationService Localizer => LocalizationService.Instance;

        public bool IsUpdateCheckRequired
        {
            get => SettingsEngine.IsUpdateCheckRequired;
            set => SettingsEngine.IsUpdateCheckRequired = value;
        }

        public string GetText(string key) => Localizer[key];

        public SettingsPage()
        {
            InitializeComponent();

            LocalizationService.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Item[]")
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Delay(100);
                        OnPropertyChanged(string.Empty);
                        UpdateComboBoxLocalization();
                    });
                }
            };

            InitializeSelections();
            UpdateComboBoxLocalization();
            SetSelectedByTag(ThemeSelector, SettingsEngine.AppTheme);

            this.Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            TintOpacitySlider.Value = SettingsEngine.AcrylicOpacity;
            LuminositySlider.Value = SettingsEngine.AcrylicLuminosity;

            var savedColor = UIHelper.ToColor(SettingsEngine.AcrylicTintColor);
            AcrylicColorPicker.Color = savedColor;
            ColorPreview.Background = new SolidColorBrush(savedColor);


            _isInitialized = true;

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

                AdvancedColorPicker.Color = global::Windows.UI.Color.FromArgb(a, r, g, b);
            }
            catch
            {
                AdvancedColorPicker.Color = global::Windows.UI.Color.FromArgb(255, 0, 120, 212);
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

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized && LanguageSelector.SelectedItem is ComboBoxItem item)
                SettingsEngine.Language = item.Tag?.ToString() ?? "en-us";
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

            if (App.Current.MainWindow is Window mainWindow)
            {
                UIHelper.ApplyBackdrop(mainWindow, selected);
            }
        }

        private void AcrylicSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (sender is Slider slider)
            {
                if (slider == TintOpacitySlider)
                    SettingsEngine.AcrylicOpacity = e.NewValue;
                else if (slider == LuminositySlider)
                    SettingsEngine.AcrylicLuminosity = e.NewValue;

                if (App.Current.MainWindow is Window window)
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

            if (App.Current.MainWindow is Window mainWindow)
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
                SettingsEngine.AppTheme = item.Tag?.ToString() ?? "Default";
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

        private async void ManualUpdateCheck_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            btn.IsEnabled = false;
            btn.Content = ResourceString.GetString("Settings_Update_Checking");

            bool updateFound = SystemDiagnostics.IsNeedUpdate;

            if (updateFound)
            {
                if (App.Current.MainWindow is MainWindow mainWin)
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

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}