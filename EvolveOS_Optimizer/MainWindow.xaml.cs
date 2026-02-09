using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using WinRT.Interop;
using AppWindow = Microsoft.UI.Windowing.AppWindow;
using System.Numerics;

namespace EvolveOS_Optimizer
{
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private AppWindow? _appWindow;
        private IntPtr _hWnd;

        public string GetText(string key) => LocalizationService.Instance[key];

        public MainWindow()
        {
            this.InitializeComponent();

            _hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            if (_appWindow != null)
            {
                _appWindow.SetIcon("Assets/EvolveOS_Optimizer.ico");
                var titleBar = _appWindow.TitleBar;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                _appWindow.Resize(new global::Windows.Graphics.SizeInt32(1575, 870));
            }

            WindowHelper.RegisterMinWidthHeight(_hWnd, 700, 400);
            UIHelper.RegisterPageTransition(RootContentControl, RootGrid);
            SetBackdrop(new MicaBackdrop());
            CenterWindow();

            LocalizationService.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Item[]")
                {
                    OnPropertyChanged(string.Empty); 
                }
            };
        }

        public void SetBackdrop(SystemBackdrop backdrop)
        {
            this.SystemBackdrop = backdrop;
        }

        public void SetBackdropByName(string name)
        {
            this.SystemBackdrop = name switch
            {
                "Mica" => new MicaBackdrop(),
                "MicaAlt" => new MicaBackdrop() { Kind = MicaKind.BaseAlt },
                "Acrylic" => new DesktopAcrylicBackdrop(),
                _ => null
            };
        }

        private void CenterWindow()
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);

                if (displayArea != null)
                {
                    var centeredPos = appWindow.Position;

                    centeredPos.X = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                    centeredPos.Y = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;

                    appWindow.Move(centeredPos);
                }
            }
        }

        public static void ApplyAccentColor(string hexColor)
        {
            try
            {
                hexColor = hexColor.Replace("#", string.Empty);
                byte a = (byte)uint.Parse(hexColor.Substring(0, 2), NumberStyles.HexNumber);
                byte r = (byte)uint.Parse(hexColor.Substring(2, 2), NumberStyles.HexNumber);
                byte g = (byte)uint.Parse(hexColor.Substring(4, 2), NumberStyles.HexNumber);
                byte b = (byte)uint.Parse(hexColor.Substring(6, 2), NumberStyles.HexNumber);

                global::Windows.UI.Color color = global::Windows.UI.Color.FromArgb(a, r, g, b);

                if (App.Current.Resources.TryGetValue("MyDynamicAccentBrush", out object brushObj)
                    && brushObj is SolidColorBrush dynamicBrush)
                {
                    dynamicBrush.Color = color;
                }
                else
                {
                    App.Current.Resources["MyDynamicAccentBrush"] = new SolidColorBrush(color);
                }

                Debug.WriteLine($"[Accent] Successfully applied color: {hexColor}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Accent] Error parsing/applying color: {ex.Message}");
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}