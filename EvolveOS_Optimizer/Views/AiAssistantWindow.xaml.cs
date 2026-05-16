// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Runtime.InteropServices;
using EvolveOS_Optimizer.Utilities.Animation;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using WinRT.Interop;

namespace EvolveOS_Optimizer.Views
{
    public sealed partial class AiAssistantWindow : Window
    {
        #region Private Fields
        private AppWindow _appWindow;
        #endregion

        #region Constructor
        public AiAssistantWindow()
        {
            this.InitializeComponent();

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            this.ExtendsContentIntoTitleBar = true;

            LoadLottieHardWay();

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = _appWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                titleBar.SetDragRectangles(new RectInt32[]
                {
                    new Windows.Graphics.RectInt32(0, 0, 0, 0)
                });
            }

            var presenter = _appWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
            }

            var mainWindow = (Application.Current as App)?.GetType().GetProperty("MainWindow")?.GetValue(Application.Current) as Window;
            if (mainWindow != null)
            {
                var mainHwnd = WindowNative.GetWindowHandle(mainWindow);
                SetWindowOwner(hwnd, mainHwnd);
            }

            CenterWindow(hwnd, 600, 450);

            UIHelper.ApplyBackdrop(this, "Acrylic");
            UIHelper.SetOverlay(true, false);

            this.Closed += AiAssistantWindow_Closed;

            UserInputBox.Loaded += (s, e) => UserInputBox.Focus(FocusState.Programmatic);
        }
        #endregion

        #region Window Management Helpers
        private void CenterWindow(IntPtr hwnd, int width, int height)
        {
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);

            double scale = UIHelper.GetScaleAdjustment(hwnd);
            int scaledWidth = (int)(width * scale);
            int scaledHeight = (int)(height * scale);

            int x = (displayArea.WorkArea.Width - scaledWidth) / 2;
            int y = (displayArea.WorkArea.Height - scaledHeight) / 2;

            _appWindow.MoveAndResize(new RectInt32(x, y, scaledWidth, scaledHeight));
        }
        #endregion

        #region Asset & Animation Loading
        private async void LoadLottieHardWay()
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string jsonPath = Path.Combine(baseDir, "Assets", "thinking_nodes.json");

                if (File.Exists(jsonPath))
                {
                    using var fileStream = File.OpenRead(jsonPath);
                    using var randomAccessStream = fileStream.AsRandomAccessStream();

                    await LottieSource.SetSourceAsync(randomAccessStream);

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        LottiePlayer.Source = LottieSource;
                        Debug.WriteLine("Lottie: Source linked and ready (Idle).");
                    });
                }
                else
                {
                    Debug.WriteLine("Lottie: File missing at " + jsonPath);
                }
            }
            catch (Exception ex) { Debug.WriteLine("Lottie Load Error: " + ex.Message); }
        }
        #endregion

        #region AI Logic & Processing
        private async void ProcessUserQuestion()
        {
            string question = UserInputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(question)) return;

            UserInputBox.IsEnabled = false;
            SendButton.IsEnabled = false;
            UserInputBox.Text = "";

            AiOutputTextBlock.Text = "";
            LottiePlayer.Visibility = Visibility.Visible;

            _ = LottiePlayer.PlayAsync(0, 1, true);

            try
            {
                string response = await AiExplainerService.ExplainGenericItemAsync(
                    itemName: question,
                    itemCategory: "General Assistant Query",
                    contextDetails: "Direct assistant interaction."
                );

                LottiePlayer.Stop();
                LottiePlayer.Visibility = Visibility.Collapsed;

                double seconds = Math.Max(1.0, response.Length / 40.0);
                TypewriterAnimation.Create(response, AiOutputTextBlock, TimeSpan.FromSeconds(seconds));
            }
            catch (Exception ex)
            {
                LottiePlayer.Stop();
                LottiePlayer.Visibility = Visibility.Collapsed;
                AiOutputTextBlock.Text = "Error: " + ex.Message;
            }
            finally
            {
                UserInputBox.IsEnabled = true;
                SendButton.IsEnabled = true;
                UserInputBox.Focus(FocusState.Programmatic);
            }
        }
        #endregion

        #region Event Handlers
        private void AiAssistantWindow_Closed(object sender, WindowEventArgs args)
        {
            UIHelper.SetOverlay(false);
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessUserQuestion();
        }

        private void UserInputBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ProcessUserQuestion();
                e.Handled = true;
            }
        }
        #endregion

        #region Win32 Window Ownership Logic

        private const int GWLP_HWNDPARENT = -8;

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private static void SetWindowOwner(IntPtr childHwnd, IntPtr ownerHwnd)
        {
            if (IntPtr.Size == 8)
            {
                SetWindowLongPtr(childHwnd, GWLP_HWNDPARENT, ownerHwnd);
            }
            else
            {
                SetWindowLong(childHwnd, GWLP_HWNDPARENT, ownerHwnd.ToInt32());
            }
        }

        #endregion
    }
}