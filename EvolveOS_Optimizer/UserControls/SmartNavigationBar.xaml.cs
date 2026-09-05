// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Pages;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using FluentIcons.Common;
using FluentIcons.WinUI;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace EvolveOS_Optimizer.UserControls
{
    public sealed partial class SmartNavigationBar : Microsoft.UI.Xaml.Controls.UserControl
    {
        private RadioButton? _selectedButton;

        private bool _isRadialMenuOpen = false;

        private string _currentRadialMenuTarget = "";

        private double _lastHeight = 0;
        private double _currentRadialAngle = 0;

        public SmartNavigationBar()
        {
            this.InitializeComponent();
        }

        public string GetText(string key) => LocalizationService.Instance[key];

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            UpdateCutoutPosition(false);
        }

        private void OnControlSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (BackgroundRect != null)
            {
                BackgroundRect.Rect = new Rect(0, 0, 55, Math.Max(e.NewSize.Height, 1000));
            }

            if (e.NewSize.Height != _lastHeight)
            {
                _lastHeight = e.NewSize.Height;

                UpdateCutoutPosition(false);
            }
        }

        private void OnNavButtonChecked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb)
            {
                _selectedButton = rb;

                this.DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateCutoutPosition(true);
                });

                CloseRadialMenu();
            }
        }

        private void UpdateCutoutPosition(bool animate)
        {
            if (_selectedButton == null || NavStackPanel == null) return;

            try
            {
                var transform = _selectedButton.TransformToVisual(this);
                Point point = transform.TransformPoint(new Point(0, 0));

                double targetY = point.Y - 20;

                if (animate)
                {
                    var anim = new DoubleAnimation
                    {
                        From = CutoutTransform.Y,
                        To = targetY,
                        Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                        EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut },
                        EnableDependentAnimation = true
                    };

                    var sb = new Storyboard();
                    Storyboard.SetTarget(anim, CutoutTransform);
                    Storyboard.SetTargetProperty(anim, "Y");

                    sb.Children.Add(anim);
                    sb.Begin();
                }
                else
                {
                    if (CutoutTransform != null)
                    {
                        CutoutTransform.Y = targetY;
                    }
                }
            }
            catch
            {
                // Silently swallow early rendering exceptions
            }
        }

        #region Radial Menu Spin Engine

        private void RadialMenuContainer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (!_isRadialMenuOpen) return;

            e.Handled = true;

            var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;

            if (delta > 0) _currentRadialAngle -= 15;
            else _currentRadialAngle += 15;

            _currentRadialAngle = Math.Clamp(_currentRadialAngle, -65, 65);

            var sb = new Storyboard();

            var containerAnim = new DoubleAnimation
            {
                To = _currentRadialAngle,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(containerAnim, RadialContainerRotate);
            Storyboard.SetTargetProperty(containerAnim, "Angle");
            sb.Children.Add(containerAnim);

            RotateTransform[] iconTransforms = { Icon1Rotate, Icon2Rotate, Icon3Rotate, Icon4Rotate, Icon5Rotate, Icon6Rotate, Icon7Rotate };
            foreach (var transform in iconTransforms)
            {
                var iconAnim = new DoubleAnimation
                {
                    To = -_currentRadialAngle,
                    Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(iconAnim, transform);
                Storyboard.SetTargetProperty(iconAnim, "Angle");
                sb.Children.Add(iconAnim);
            }

            sb.Begin();
        }

        #endregion

        #region Radial Menu Population & Click Handlers

        private void OnNavButtonRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is RadioButton rb)
            {
                string target = rb.CommandParameter?.ToString() ?? "";

                if (target != "SystemManager" && target != "Software" && target != "Customize" && target != "Optimize")
                {
                    return;
                }

                if (NavStackPanel != null)
                {
                    foreach (var child in NavStackPanel.Children)
                    {
                        if (child is RadioButton sideRb)
                        {
                            VisualStateManager.GoToState(sideRb, "Normal", true);
                        }
                    }
                    NavStackPanel.IsHitTestVisible = false;
                }

                _currentRadialMenuTarget = target;

                SetupRadialButton(SubBtn1, "", "", null, false);
                SetupRadialButton(SubBtn2, "", "", null, false);
                SetupRadialButton(SubBtn3, "", "", null, false);
                SetupRadialButton(SubBtn4, "", "", null, false);
                SetupRadialButton(SubBtn5, "", "", null, false);
                SetupRadialButton(SubBtn6, "", "", null, false);
                SetupRadialButton(SubBtn7, "", "", null, false);

                int buttonCount = 0;

                if (target == "SystemManager")
                {
                    SetupRadialButton(SubBtn1, "ProcessManagerPage", ResourceString.GetString("Nav_ProcessManager"), new FontIcon { Glyph = "\xEA37", FontSize = 16 });
                    SetupRadialButton(SubBtn2, "ServiceManagerPage", ResourceString.GetString("Nav_ServiceManager"), new FontIcon { Glyph = "\xE90F", FontSize = 16 });
                    SetupRadialButton(SubBtn3, "StartupManagerPage", ResourceString.GetString("Nav_StartupManager"), new FontIcon { Glyph = "\xE718", FontSize = 16 });
                    buttonCount = 3;
                }
                else if (target == "Software")
                {
                    SetupRadialButton(SubBtn1, "SystemAppsPage", ResourceString.GetString("Nav_SystemApps"), new FontIcon { Glyph = "\xE71D", FontSize = 16 });
                    SetupRadialButton(SubBtn2, "AppStorePage", ResourceString.GetString("Nav_AppStore"), new FontIcon { Glyph = "\xE896", FontSize = 16 });
                    SetupRadialButton(SubBtn3, "PackagesPage", ResourceString.GetString("Nav_Packages"), new FontIcon { Glyph = "\xE735", FontSize = 16 });
                    buttonCount = 3;
                }
                else if (target == "Customize")
                {
                    SetupRadialButton(SubBtn1, "Explorer", ResourceString.GetString("Nav_Explorer"), new Viewbox { Width = 16, Height = 16, Child = new PathIcon { Data = GetIconGeometry("ExplorerIconPath") } });
                    SetupRadialButton(SubBtn2, "StartMenu", ResourceString.GetString("Nav_StartMenu"), new Viewbox { Width = 16, Height = 16, Child = new PathIcon { Data = GetIconGeometry("StartMenuIconPath") } });
                    SetupRadialButton(SubBtn3, "Taskbar", ResourceString.GetString("Nav_Taskbar"), new Viewbox { Width = 16, Height = 16, Child = new PathIcon { Data = GetIconGeometry("TaskbarIconPath") } });
                    SetupRadialButton(SubBtn4, "WindowsTheme", ResourceString.GetString("Nav_WindowsTheme"), new Viewbox { Width = 16, Height = 16, Child = new PathIcon { Data = GetIconGeometry("WindowsThemeIconPath") } });
                    buttonCount = 4;
                }
                else if (target == "Optimize")
                {
                    SetupRadialButton(SubBtn1, "Sound", ResourceString.GetString("Nav_Sound"), new FluentIcon { Icon = Icon.Speaker2, IconVariant = IconVariant.Regular, FontSize = 16 });
                    SetupRadialButton(SubBtn2, "Update", ResourceString.GetString("Nav_Update"), new FluentIcon { Icon = Icon.ArrowSync, IconVariant = IconVariant.Regular, FontSize = 16 });
                    SetupRadialButton(SubBtn3, "Notification", ResourceString.GetString("Nav_Notification"), new Viewbox { Width = 16, Height = 16, Child = new PathIcon { Data = GetIconGeometry("NotificationIconPath") } });
                    SetupRadialButton(SubBtn4, "Privacy", ResourceString.GetString("Nav_Privacy"), new Viewbox { Width = 16, Height = 16, Child = new PathIcon { Data = GetIconGeometry("PrivacyIconPath") } });
                    SetupRadialButton(SubBtn5, "Power", ResourceString.GetString("Nav_Power"), new Viewbox { Width = 16, Height = 16, Child = new PathIcon { Data = GetIconGeometry("PowerIconPath") } });
                    SetupRadialButton(SubBtn6, "Gaming", ResourceString.GetString("Nav_Gaming"), new Viewbox { Width = 16, Height = 16, Child = new PathIcon { Data = GetIconGeometry("GamingIconPath") } });
                    SetupRadialButton(SubBtn7, "Advanced", ResourceString.GetString("Nav_Advanced"), new FluentIcon { Icon = Icon.Wrench, IconVariant = IconVariant.Regular, FontSize = 16 });
                    buttonCount = 7;
                }

                var transform = rb.TransformToVisual(this);
                Point position = transform.TransformPoint(new Point(0, 0));

                RadialContainerTranslate.Y = position.Y - 80;

                _currentRadialAngle = 0;
                RadialContainerRotate.Angle = 0;

                Icon1Rotate.Angle = 0; Icon2Rotate.Angle = 0; Icon3Rotate.Angle = 0;
                Icon4Rotate.Angle = 0; Icon5Rotate.Angle = 0; Icon6Rotate.Angle = 0; Icon7Rotate.Angle = 0;

                RadialMenuHitBox.Opacity = 1;
                RadialMenuHitBox.IsHitTestVisible = true;
                _isRadialMenuOpen = true;

                if (this.XamlRoot?.Content != null)
                {
                    this.XamlRoot.Content.PointerPressed -= XamlRoot_PointerPressed;
                    this.XamlRoot.Content.PointerPressed += XamlRoot_PointerPressed;
                }

                SubBtn1Translate.X = 0; SubBtn1Translate.Y = 0;
                SubBtn2Translate.X = 0; SubBtn2Translate.Y = 0;
                SubBtn3Translate.X = 0; SubBtn3Translate.Y = 0;
                SubBtn4Translate.X = 0; SubBtn4Translate.Y = 0;
                SubBtn5Translate.X = 0; SubBtn5Translate.Y = 0;
                SubBtn6Translate.X = 0; SubBtn6Translate.Y = 0;
                SubBtn7Translate.X = 0; SubBtn7Translate.Y = 0;

                BeginRadialAnimation(buttonCount);
            }
        }

        private void BeginRadialAnimation(int buttonCount)
        {
            // Define final coordinates for each button count
            (double X, double Y)[] coords = buttonCount switch
            {
                2 => new[] { (61.0, -35.0), (61.0, 35.0) },
                3 => new[] { (45.0, -45.0), (65.0, 0.0), (45.0, 45.0) },
                4 => new[] { (38.0, -65.0), (70.0, -25.0), (70.0, 25.0), (38.0, 65.0) },
                5 => new[] { (23.0, -87.0), (71.0, -55.0), (90.0, 0.0), (71.0, 55.0), (23.0, 87.0) },
                6 => new[] { (23.0, -87.0), (64.0, -64.0), (87.0, -23.0), (87.0, 23.0), (64.0, 64.0), (23.0, 87.0) },
                7 => new[] { (0.0, -96.0), (48.0, -83.0), (83.0, -48.0), (96.0, 0.0), (83.0, 48.0), (48.0, 83.0), (0.0, 96.0) },
                _ => Array.Empty<(double, double)>()
            };

            TranslateTransform[] translates = { SubBtn1Translate, SubBtn2Translate, SubBtn3Translate, SubBtn4Translate, SubBtn5Translate, SubBtn6Translate, SubBtn7Translate };
            var sb = new Storyboard();

            for (int i = 0; i < Math.Min(coords.Length, translates.Length); i++)
            {
                var targetTranslate = translates[i];
                var targetCoord = coords[i];
                double durationSec = 0.2 + (i * 0.02);

                var animX = new DoubleAnimation
                {
                    From = 0,
                    To = targetCoord.X,
                    Duration = TimeSpan.FromSeconds(durationSec),
                    EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(animX, targetTranslate);
                Storyboard.SetTargetProperty(animX, "X");
                sb.Children.Add(animX);

                var animY = new DoubleAnimation
                {
                    From = 0,
                    To = targetCoord.Y,
                    Duration = TimeSpan.FromSeconds(durationSec),
                    EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(animY, targetTranslate);
                Storyboard.SetTargetProperty(animY, "Y");
                sb.Children.Add(animY);
            }

            sb.Begin();
        }

        private void CloseRadialMenu()
        {
            RadialMenuHitBox.Opacity = 0;
            RadialMenuHitBox.IsHitTestVisible = false;
            _isRadialMenuOpen = false;

            if (this.XamlRoot?.Content != null)
            {
                this.XamlRoot.Content.PointerPressed -= XamlRoot_PointerPressed;
            }

            if (NavStackPanel != null)
            {
                NavStackPanel.IsHitTestVisible = true;
            }
        }

        private void SetupRadialButton(Button btn, string tag, string tooltip, FrameworkElement? iconElement, bool isVisible = true)
        {
            btn.Tag = tag;
            btn.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

            ToolTipService.SetToolTip(btn, tooltip);
            ToolTipService.SetPlacement(btn, PlacementMode.Right);

            if (btn.Content is Grid container)
            {
                container.Children.Clear();
                if (iconElement != null)
                {
                    container.Children.Add(iconElement);
                }
            }
        }

        private void SubMenuBtn_Click(object sender, RoutedEventArgs e)
        {
            CloseRadialMenu();

            if (sender is Button btn && btn.Tag is string requestedPane)
            {
                if (_currentRadialMenuTarget == "SystemManager")
                    SystemManagerPage.RequestedPaneOnLoad = requestedPane;
                else if (_currentRadialMenuTarget == "Software")
                    SoftwareCenterPage.RequestedPaneOnLoad = requestedPane;
                else if (_currentRadialMenuTarget == "Customize")
                    WinCustomizePage.RequestedSectionOnLoad = requestedPane;
                else if (_currentRadialMenuTarget == "Optimize")
                    WinOptimizePage.RequestedSectionOnLoad = requestedPane;

                if (this.DataContext is MainWinViewModel vm)
                {
                    vm.ExecuteNavigateCommand?.Execute(_currentRadialMenuTarget);
                    UpdateActiveTab(_currentRadialMenuTarget);
                }
            }
        }

        private void XamlRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!_isRadialMenuOpen) return;

            DependencyObject? element = e.OriginalSource as DependencyObject;
            bool clickedOnRadialButton = false;

            while (element != null)
            {
                if (element is Button btn && btn.Name.StartsWith("SubBtn"))
                {
                    clickedOnRadialButton = true;
                    break;
                }

                element = VisualTreeHelper.GetParent(element);
            }

            if (!clickedOnRadialButton)
            {
                CloseRadialMenu();
            }
        }

        private Geometry? GetIconGeometry(string resourceKey)
        {
            object? res = null;

            if (this.Resources.TryGetValue(resourceKey, out var localRes))
                res = localRes;
            else if (Application.Current.Resources.TryGetValue(resourceKey, out var appRes))
                res = appRes;

            if (res is Geometry geom)
                return geom;

            if (res is string pathString)
            {
                try
                {
                    return Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry), pathString) as Geometry;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        #endregion

        public void UpdateActiveTab(string tag)
        {
            if (tag == "Home" && BtnNavHome != null) BtnNavHome.IsChecked = true;
            else if (tag == "Diagnostics" && BtnNavDiagnostics != null) BtnNavDiagnostics.IsChecked = true;
            else if (tag == "SystemManager" && BtnNavSystemManager != null) BtnNavSystemManager.IsChecked = true;
            else if (tag == "SystemCleaner" && BtnNavSystemCleaner != null) BtnNavSystemCleaner.IsChecked = true;
            else if (tag == "Software" && BtnNavSoftware != null) BtnNavSoftware.IsChecked = true;
            else if (tag == "GroupPolicy" && BtnNavGroupPolicy != null) BtnNavGroupPolicy.IsChecked = true;
            else if (tag == "RegistryEditor" && BtnNavRegEditor != null) BtnNavRegEditor.IsChecked = true;
            else if (tag == "Optimize" && BtnNavOptimize != null) BtnNavOptimize.IsChecked = true;
            else if (tag == "Customize" && BtnNavCustomize != null) BtnNavCustomize.IsChecked = true;
            else if (tag == "Utilities" && BtnNavUtilities != null) BtnNavUtilities.IsChecked = true;
            else if (tag == "Scripts" && BtnNavScripts != null) BtnNavScripts.IsChecked = true;
            else if (tag == "Settings" && BtnNavSettings != null) BtnNavSettings.IsChecked = true;
            else if (tag == "ProfileBuilder" && BtnNavProfileBuilder != null) BtnNavProfileBuilder.IsChecked = true;
            else if (tag == "UserAccounts" && BtnNavUserAccounts != null) BtnNavUserAccounts.IsChecked = true;
        }
    }
}