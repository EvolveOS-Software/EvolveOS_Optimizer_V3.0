using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SettingsPage : Page, INotifyPropertyChanged
    {
        private bool _isInitialized;
        private string _pendingHexColor = "#FF0078D4";

        public event PropertyChangedEventHandler? PropertyChanged;
        public LocalizationService Localizer => LocalizationService.Instance;

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
            _isInitialized = true;
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

                // Use global:: to avoid the namespace collision
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
            if (_isInitialized && BackdropSelector.SelectedItem is ComboBoxItem item)
                SettingsEngine.Backdrop = item.Tag?.ToString() ?? "None";
        }

        private void ColorPalette_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitialized && ColorPalette.SelectedItem is Rectangle rect && rect.Tag != null)
                SettingsEngine.AccentColor = rect.Tag.ToString()!;
        }

        private void AdvancedColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args) =>
            _pendingHexColor = $"#{args.NewColor.A:X2}{args.NewColor.R:X2}{args.NewColor.G:X2}{args.NewColor.B:X2}";

        private void ApplyCustomColor_Click(object sender, RoutedEventArgs e)
        {
            ColorPalette.SelectedItem = null;
            SettingsEngine.AccentColor = _pendingHexColor;
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}