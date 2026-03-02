// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Configuration;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace EvolveOS_Optimizer.Views
{
    public sealed partial class UserLoginWindow : Window
    {
        private UserLoginViewModel? ViewModel => LoginView.DataContext as UserLoginViewModel;

        public UserLoginWindow(WeatherService weatherService, string? reasonMessage = null)
        {
            this.InitializeComponent();

            CenterAndSizeWindow(850, 600);

            var loginVM = new UserLoginViewModel(weatherService, () =>
            {
                this.AppWindow.Hide();
            });

            loginVM.SwitchToCreateAccountRequested += () => BtnGoToCreateAccount_Click(this, new RoutedEventArgs());
            LoginView.DataContext = loginVM;

            var createVM = new UserCreateViewModel { CloseAction = this.Close };
            createVM.SwitchToLoginRequested += () => BtnGoToLogin_Click(this, new RoutedEventArgs());
            CreateAccountView.DataContext = createVM;

            ApplyUserAccentColor();
            UIHelper.ApplyBackdrop(this, SettingsEngine.Backdrop);

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            LoginPassword.IsEnabled = false;

            if (!string.IsNullOrEmpty(reasonMessage))
            {
                TxtStatusMessage.Text = reasonMessage;
                StatusBorder.Visibility = Visibility.Visible;
            }

            this.Activated += UserLoginWindow_Activated;
        }

        #region Window Sizing & Centering

        private void CenterAndSizeWindow(int width, int height)
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(myWndId);

            appWindow.Resize(new SizeInt32 { Width = width, Height = height });

            DisplayArea displayArea = DisplayArea.GetFromWindowId(myWndId, DisplayAreaFallback.Nearest);

            if (displayArea != null)
            {
                var centeredPosition = new PointInt32
                {
                    X = ((displayArea.WorkArea.Width - width) / 2) + displayArea.WorkArea.X,
                    Y = ((displayArea.WorkArea.Height - height) / 2) + displayArea.WorkArea.Y
                };

                appWindow.Move(centeredPosition);
            }
        }

        #endregion

        #region Theming & Accent Colors

        private void ApplyUserAccentColor()
        {
            try
            {
                string hexColor = SettingsEngine.AccentColor;
                Color userColor = ColorFromHex(hexColor);

                if (this.Content is FrameworkElement root && root.Resources != null)
                {
                    if (root.Resources.TryGetValue("Brush_Accent", out object? brushObj) && brushObj is SolidColorBrush accentBrush)
                    {
                        accentBrush.Color = userColor;
                    }

                    root.Resources["SystemAccentColor"] = userColor;
                }
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(ex, "UserLogin_ApplyUserAccentColor_Fail");
            }
        }

        private Color ColorFromHex(string hex)
        {
            hex = hex.Replace("#", string.Empty);
            byte a = 255;
            int pos = 0;

            if (hex.Length == 8)
            {
                a = byte.Parse(hex.Substring(pos, 2), System.Globalization.NumberStyles.HexNumber);
                pos += 2;
            }

            byte r = byte.Parse(hex.Substring(pos, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(pos + 2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(pos + 4, 2), System.Globalization.NumberStyles.HexNumber);

            return Color.FromArgb(a, r, g, b);
        }

        #endregion

        #region Window LifeCycle

        private async void UserLoginWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            this.Activated -= UserLoginWindow_Activated;

            await Task.Delay(100);

            LoginUserName.Focus(FocusState.Programmatic);

            if (ViewModel != null && this.Content?.XamlRoot != null)
            {
                await ViewModel.InitialDatabaseCheckAsync(this.Content.XamlRoot);
            }
            else
            {
                ViewModel?.RestartTimer();
            }
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e) => this.Close();

        #endregion

        #region UI Interactions & Animations

        private void ButtonSettings_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsMenu.Width == 0 || SettingsMenu.Width == 380)
            {
                SettingsMenuAnimation();
            }
        }

        private void SettingsMenuAnimation()
        {
            var visual = ElementCompositionPreview.GetElementVisual(SettingsMenu);
            var compositor = visual.Compositor;

            bool isOpening = SettingsMenu.Width == 0;
            SettingsMenu.Width = isOpening ? 380 : 0;

            var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
            offsetAnimation.InsertKeyFrame(1f, new System.Numerics.Vector3(isOpening ? 0 : 380, 0, 0));
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(400);

            visual.StartAnimation("Offset", offsetAnimation);
        }

        #endregion

        #region Authentication Logic (Sync Fixes)

        private void LoginUserName_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoginPassword.IsEnabled = LoginUserName.Text.Length >= 2;

            if (ViewModel != null)
            {
                ViewModel.Username = LoginUserName.Text;
            }
        }

        private void LoginPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.Password = LoginPassword.Password;
            }
        }

        private void CreateAccountPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (CreateAccountView.DataContext is UserCreateViewModel vm)
            {
                vm.PasswordText = CreateAccountPassword.Password;
            }
        }

        private void CreateAccountConfirmPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (CreateAccountView.DataContext is UserCreateViewModel vm)
            {
                vm.ConfirmPasswordText = CreateAccountConfirmPassword.Password;
            }
        }

        private void LoginUserName_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space)
            {
                e.Handled = true;
            }
        }

        private void LoginPassword_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && ViewModel?.SignInCommand.CanExecute(null) == true)
            {
                ViewModel.SignInCommand.Execute(null);
            }
        }

        private void BtnRunOnStartUp_ChangedState(object sender, RoutedEventArgs e) => SettingsEngine.IsRunOnStartUp = BtnRunOnStartUp.IsOn;

        #endregion

        #region View Navigation (Sliding Animation)

        private void BtnGoToCreateAccount_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.StopTimer();

            ButtonSettings.Visibility = Visibility.Collapsed;

            if (SettingsMenu.Width > 0)
            {
                SettingsMenuAnimation();
            }

            if (CreateAccountView.DataContext == null)
            {
                var vm = new UserCreateViewModel { CloseAction = this.Close };
                vm.SwitchToLoginRequested += () => BtnGoToLogin_Click(this, new RoutedEventArgs());

                CreateAccountView.DataContext = vm;
            }
            AnimateViewTransition(LoginView, CreateAccountView, true);
        }

        private void BtnGoToLogin_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.RestartTimer();

            ButtonSettings.Visibility = Visibility.Visible;

            AnimateViewTransition(CreateAccountView, LoginView, false);
        }

        private void AnimateViewTransition(UIElement viewToHide, UIElement viewToShow, bool slidingLeft)
        {
            Canvas.SetZIndex(viewToHide, 0);
            Canvas.SetZIndex(viewToShow, 1);

            viewToShow.Visibility = Visibility.Visible;
            viewToShow.IsHitTestVisible = true;
            viewToHide.IsHitTestVisible = false;

            ElementCompositionPreview.SetIsTranslationEnabled(viewToHide, true);
            ElementCompositionPreview.SetIsTranslationEnabled(viewToShow, true);

            var hideVisual = ElementCompositionPreview.GetElementVisual(viewToHide);
            var showVisual = ElementCompositionPreview.GetElementVisual(viewToShow);
            var compositor = hideVisual.Compositor;

            float offset = slidingLeft ? 150f : -150f;

            showVisual.Properties.InsertVector3("Translation", new System.Numerics.Vector3(offset, 0, 0));
            showVisual.Opacity = 0f;

            var slideIn = compositor.CreateScalarKeyFrameAnimation();
            slideIn.InsertKeyFrame(1.0f, 0f);
            slideIn.Duration = TimeSpan.FromMilliseconds(400);

            var fadeIn = compositor.CreateScalarKeyFrameAnimation();
            fadeIn.InsertKeyFrame(1.0f, 1.0f);
            fadeIn.Duration = TimeSpan.FromMilliseconds(300);

            var fadeOut = compositor.CreateScalarKeyFrameAnimation();
            fadeOut.InsertKeyFrame(1.0f, 0.0f);
            fadeOut.Duration = TimeSpan.FromMilliseconds(300);

            var batch = compositor.CreateScopedBatch(Microsoft.UI.Composition.CompositionBatchTypes.Animation);

            hideVisual.StartAnimation("Opacity", fadeOut);
            showVisual.StartAnimation("Translation.X", slideIn);
            showVisual.StartAnimation("Opacity", fadeIn);

            batch.Completed += (s, ev) =>
            {
                viewToHide.Visibility = Visibility.Collapsed;
                hideVisual.Properties.InsertVector3("Translation", new System.Numerics.Vector3(0, 0, 0));
            };
            batch.End();
        }

        #endregion
    }
}