// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

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
        private AppWindow _appWindow;

        public AiAssistantWindow()
        {
            this.InitializeComponent();

            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            this.ExtendsContentIntoTitleBar = true;

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

        private void AiAssistantWindow_Closed(object sender, WindowEventArgs args)
        {
            UIHelper.SetOverlay(false);
        }

        private async void ProcessUserQuestion()
        {
            string question = UserInputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(question)) return;

            UserInputBox.IsEnabled = false;
            SendButton.IsEnabled = false;
            UserInputBox.Text = "";

            string thinkingText = ResourceString.GetString("ai_explainer_thinking") ?? "Thinking...";
            TypewriterAnimation.Create(thinkingText, AiOutputTextBlock, TimeSpan.FromSeconds(0.5));

            try
            {
                string response = await AiExplainerService.ExplainGenericItemAsync(
                    itemName: question,
                    itemCategory: "General System Inquiry",
                    contextDetails: "The user is asking a direct question. Answer it directly and conversationally."
                );

                double seconds = Math.Max(1.0, response.Length / 40.0);
                TypewriterAnimation.Create(response, AiOutputTextBlock, TimeSpan.FromSeconds(seconds));
            }
            finally
            {
                UserInputBox.IsEnabled = true;
                SendButton.IsEnabled = true;
                UserInputBox.Focus(FocusState.Programmatic);
            }
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