using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using System;
using System.Globalization;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SettingsPage : Page
    {
        private readonly bool _isInitialized = false;
        private string _pendingHexColor = "#FF0078D4";

        public SettingsPage()
        {
            InitializeComponent();

            string savedBackdrop = SettingsEngine.Backdrop;
            foreach (ComboBoxItem item in BackdropSelector.Items)
            {
                string content = item.Content?.ToString() switch
                {
                    "Mica Alt" => "MicaAlt",
                    "Desktop Acrylic" => "Acrylic",
                    "None (Solid)" => "None",
                    null => "",
                    _ => item.Content?.ToString() ?? ""
                };

                if (content == savedBackdrop)
                {
                    BackdropSelector.SelectedItem = item;
                    break;
                }
            }

            string savedColor = SettingsEngine.AccentColor;
            foreach (var item in ColorPalette.Items)
            {
                if (item is Rectangle rect && rect.Tag?.ToString() == savedColor)
                {
                    ColorPalette.SelectedItem = item;
                    break;
                }
            }

            try
            {
                string hex = savedColor.Replace("#", string.Empty);
                byte a = (byte)uint.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                byte r = (byte)uint.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                byte g = (byte)uint.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                byte b = (byte)uint.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);

                Color color = Color.FromArgb(a, r, g, b);
                AdvancedColorPicker.Color = color;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UI] Hex Parse Error: {ex.Message}");
                AdvancedColorPicker.Color = Color.FromArgb(255, 0, 120, 212);
            }

            _isInitialized = true;
        }

        private void BackdropSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (BackdropSelector?.SelectedItem is ComboBoxItem item)
            {
                string selected = item.Content?.ToString() ?? "";

                string engineValue = selected switch
                {
                    "Mica Alt" => "MicaAlt",
                    "Desktop Acrylic" => "Acrylic",
                    "None (Solid)" => "None",
                    _ => selected
                };

                SettingsEngine.Backdrop = engineValue;
                System.Diagnostics.Debug.WriteLine($"[UI] User changed backdrop to: {engineValue}");
            }
        }

        private void ColorPalette_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (sender is GridView gv && gv.SelectedItem is Rectangle rect)
            {
                if (rect.Tag != null)
                {
                    string hexColor = rect.Tag.ToString()!;
                    System.Diagnostics.Debug.WriteLine($"Selected Hex: {hexColor}");

                    SettingsEngine.AccentColor = hexColor;

                    try
                    {
                        string hex = hexColor.Replace("#", string.Empty);
                        byte a = (byte)uint.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                        byte r = (byte)uint.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                        byte g = (byte)uint.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                        byte b = (byte)uint.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                        AdvancedColorPicker.Color = Color.FromArgb(a, r, g, b);
                    }
                    catch { /* Ignore sync parse errors */ }
                }
            }
        }

        private void AdvancedColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            _pendingHexColor = string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}",
                args.NewColor.A,
                args.NewColor.R,
                args.NewColor.G,
                args.NewColor.B);
        }

        private void ApplyCustomColor_Click(object sender, RoutedEventArgs e)
        {
            ColorPalette.SelectedItem = null;

            SettingsEngine.AccentColor = _pendingHexColor;
            System.Diagnostics.Debug.WriteLine($"[UI] Applied Advanced Color: {_pendingHexColor}");
        }
    }
}