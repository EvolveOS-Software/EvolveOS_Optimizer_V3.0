using EvolveOS_Optimizer.Utilities.Controls;
using Microsoft.UI.Xaml;
using System;

namespace EvolveOS_Optimizer
{
    public partial class App : Application
    {
        public Window? MainWindow { get; private set; }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();

            SettingsEngine.CheckingParameters();

            string savedColor = SettingsEngine.AccentColor;
            UpdateGlobalAccentColor(savedColor);

            MainWindow.Activate();
        }

        private void UpdateGlobalAccentColor(string hexColor)
        {
            try
            {
                if (string.IsNullOrEmpty(hexColor)) return;

                string hex = hexColor.Replace("#", string.Empty);

                if (hex.Length == 6) hex = "FF" + hex;

                byte a = (byte)uint.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                byte r = (byte)uint.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = (byte)uint.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = (byte)uint.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);

                Windows.UI.Color color = Windows.UI.Color.FromArgb(a, r, g, b);

                if (Application.Current.Resources.ContainsKey("MyDynamicAccentBrush"))
                {
                    var brush = (Microsoft.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["MyDynamicAccentBrush"];
                    brush.Color = color;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Failed to load startup accent: {ex.Message}");
            }
        }
    }
}
