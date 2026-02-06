using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.UI.Xaml.Controls;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SettingsPage : Page
    {
        private readonly bool _isInitialized = false;

        public SettingsPage()
        {
            InitializeComponent();

            string saved = SettingsEngine.Backdrop;

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

                if (content == saved)
                {
                    BackdropSelector.SelectedItem = item;
                    break;
                }
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
    }
}
