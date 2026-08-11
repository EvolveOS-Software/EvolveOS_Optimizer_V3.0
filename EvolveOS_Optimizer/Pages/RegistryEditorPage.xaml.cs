// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Assets.UserControl;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class RegistryEditorPage : Page, IPurgeable
    {
        private Dictionary<NavigationViewItem, RegistryWorkspace> _workspaces = new();
        private int _workspaceCount = 0;

        #region Constructor
        public RegistryEditorPage()
        {
            this.InitializeComponent();

            if (SettingsEngine.IsHighPerformanceModeEnabled)
            {
                this.NavigationCacheMode = NavigationCacheMode.Required;
            }
            else
            {
                this.NavigationCacheMode = NavigationCacheMode.Disabled;
            }

            Loaded += RegistryEditorPage_Loaded;
            Unloaded += RegistryEditorPage_Unloaded;

            AddNewWorkspaceTab();
        }
        #endregion

        #region Lifecycle Events
        private async void RegistryEditorPage_Loaded(object sender, RoutedEventArgs e)
        {
            AiExplainerService.PreWarmConnection();

            await ShowRegistryWarningDialogAsync();
        }

        private void RegistryEditorPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _ = Purge();
        }
        #endregion

        #region Tab Management (NavigationView Implementation)

        private void AddWorkspaceButton_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            AddNewWorkspaceTab();
        }

        private void WorkspaceNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem && _workspaces.ContainsKey(selectedItem))
            {
                WorkspaceContainer.Content = _workspaces[selectedItem];
            }
        }

        private void AddNewWorkspaceTab()
        {
            _workspaceCount++;
            string headerText = $"{ResourceString.GetString("registry_editor_workspace_prefix")} {_workspaceCount}";

            var newWorkspace = new RegistryWorkspace()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var navItem = new NavigationViewItem
            {
                Content = headerText,
                Icon = new SymbolIcon(Symbol.Folder),
                FontFamily = (FontFamily)Application.Current.Resources["Jura-SemiBold"]
            };

            var flyout = new MenuFlyout();
            var closeItem = new MenuFlyoutItem { Text = ResourceString.GetString("registry_editor_close_workspace") };

            closeItem.Icon = new FontIcon { Glyph = "\uE711" };

            closeItem.Click += (s, e) => CloseWorkspace(navItem);
            flyout.Items.Add(closeItem);

            navItem.ContextFlyout = flyout;

            _workspaces.Add(navItem, newWorkspace);
            WorkspaceNavView.MenuItems.Add(navItem);

            WorkspaceNavView.SelectedItem = navItem;
        }

        private void CloseWorkspace(NavigationViewItem itemToClose)
        {
            if (WorkspaceNavView.MenuItems.Count > 1)
            {
                if ((NavigationViewItem)WorkspaceNavView.SelectedItem == itemToClose)
                {
                    int index = WorkspaceNavView.MenuItems.IndexOf(itemToClose);
                    int nextIndex = index > 0 ? index - 1 : index + 1;
                    WorkspaceNavView.SelectedItem = WorkspaceNavView.MenuItems[nextIndex];
                }

                WorkspaceNavView.MenuItems.Remove(itemToClose);
                _workspaces.Remove(itemToClose);
            }
            else
            {
                App.ShowNotification(
                    ResourceString.GetString("registry_editor_notice_title"),
                    ResourceString.GetString("registry_editor_notice_min_workspace"),
                    Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational,
                    3000);
            }
        }
        #endregion

        #region Warning Dialog
        private async Task ShowRegistryWarningDialogAsync()
        {
            if (LocalMachineSettingsEngine.HideRegistryWarning) return;

            var stackPanel = new StackPanel { Spacing = 8 };

            stackPanel.Children.Add(new TextBlock
            {
                Text = ResourceString.GetString("registry_editor_warn_desc1"),
                TextWrapping = TextWrapping.Wrap
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = ResourceString.GetString("registry_editor_warn_pro_tips"),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 12, 0, 0)
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = ResourceString.GetString("registry_editor_warn_desc2"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            var dontShowAgainCheckbox = new CheckBox
            {
                Content = ResourceString.GetString("registry_editor_warn_dont_show"),
                Margin = new Thickness(0, 16, 0, 0)
            };
            stackPanel.Children.Add(dontShowAgainCheckbox);

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("registry_editor_warn_title"),
                Content = stackPanel,
                PrimaryButtonText = ResourceString.GetString("registry_editor_warn_btn_primary"),
                CloseButtonText = ResourceString.GetString("registry_editor_warn_btn_close"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (dontShowAgainCheckbox.IsChecked == true)
                {
                    LocalMachineSettingsEngine.HideRegistryWarning = true;
                }
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (MainWindow.Instance?.RootGrid.DataContext is MainWinViewModel vm)
                    {
                        MainWindow.Instance.SwitchPage(vm.PreviousViewTag);
                    }
                });
            }
        }
        #endregion

        #region Purge Page
        public Task Purge()
        {
            Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and Workspaces...");

                Loaded -= RegistryEditorPage_Loaded;
                Unloaded -= RegistryEditorPage_Unloaded;

                _ = Task.Run(async () =>
                {
                    await Task.Delay(350);

                    DispatcherQueue?.TryEnqueue(() =>
                    {
                        WorkspaceNavView.MenuItems.Clear();
                        _workspaces.Clear();
                        WorkspaceContainer.Content = null;

                        //this.Bindings?.StopTracking();
                        this.DataContext = null;
                        this.Content = null;
                    });

                    DiagnosticsPageViewModel.Current?.ForceImmediateMemoryCleanup();
                });
            }
            else
            {
                Debug.WriteLine($"[{this.GetType().Name}] High Performance Mode: State preserved in RAM cache.");
            }

            return Task.CompletedTask;
        }
        #endregion
    }
}