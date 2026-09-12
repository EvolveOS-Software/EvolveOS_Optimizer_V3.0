// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Security.AccessControl;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Input;
using Vanara.PInvoke;
using Windows.Foundation.Metadata;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class SecurityAdvancedPage : Page, IPurgeable
    {
        #region Properties
        public SecurityAdvancedViewModel ViewModel { get; set; } = new SecurityAdvancedViewModel();
        public AppWindow? AppWindow;
        #endregion

        #region Constructor
        public SecurityAdvancedPage()
        {
            InitializeComponent();

            this.Unloaded += SecurityAdvancedPage_Unloaded;
        }

        private void SecurityAdvancedPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _ = Purge();
        }
        #endregion

        #region Navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is KeyItem keyItem)
            {
                ViewModel.KeyItem = keyItem;

                ViewModel.LoadKeySecurityOwner();
                ViewModel.GetKeyAccessControlList();
            }
            else
            {
                Debug.WriteLine("Warning: Navigation failed to provide a valid KeyItem.");
            }
        }
        #endregion

        #region Permission Management
        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedPrincipal?.AccessRuleAdvanced?.RawAceFlags.HasFlag(AceFlags.Inherited) == true)
            {
                ShowErrorDialog("You cannot remove inherited permissions.", "To remove inherited permissions, you must disable inheritance first.");
                return;
            }

            ViewModel.RemoveSelectedPrincipal();
        }

        private async void DisableInheritanceButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsInheritanceDisabled)
            {
                ViewModel.ApplyEnableInheritance();
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Block Inheritance",
                Content = "What do you want to do with the current inherited permissions?\n\n" +
                          "• Convert: Copies the inherited permissions to explicit permissions on this object.\n" +
                          "• Remove: Removes the inherited permissions from this object entirely.",
                PrimaryButtonText = "Convert",
                SecondaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                ViewModel.ApplyDisableInheritance(copyInheritedRules: true);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                ViewModel.ApplyDisableInheritance(copyInheritedRules: false);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = IntPtr.Zero;
            if (this.XamlRoot != null && this.XamlRoot.ContentIslandEnvironment != null)
            {
                var windowId = this.XamlRoot.ContentIslandEnvironment.AppWindowId;
                hwnd = Win32Interop.GetWindowFromWindowId(windowId);
            }

            if (hwnd == IntPtr.Zero)
            {
                ShowErrorDialog("Error", "Could not locate the window handle to display the Object Picker.");
                return;
            }

            string? selectedName = WindowsObjectPicker.ShowDialog(hwnd);

            if (!string.IsNullOrWhiteSpace(selectedName))
            {
                var result = ViewModel.AddNewPrincipal(selectedName);

                if (result.Failed)
                {
                    ShowErrorDialog("Name Not Found", $"An object named '{selectedName}' cannot be found. Check the spelling and try again.");
                }
            }
        }
        #endregion

        #region Dialogs & UI Helpers
        private async void ShowErrorDialog(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void AdvancedPermissionListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            // Future implementation
        }
        #endregion

        #region Window Management
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            var result = ViewModel.SaveKeySecurity();

            if (result.Succeeded)
            {
                CloseCurrentWindow();
            }
            else
            {
                Debug.WriteLine($"Failed to save permissions: {result.FormatMessage()}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CloseCurrentWindow();
        }

        private void CloseCurrentWindow()
        {
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                if (AppWindow != null)
                {
                    AppWindow.Destroy();
                    return;
                }

                if (this.XamlRoot != null && this.XamlRoot.ContentIslandEnvironment != null)
                {
                    var windowId = this.XamlRoot.ContentIslandEnvironment.AppWindowId;
                    var currentAppWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    currentAppWindow?.Destroy();
                }
                else
                {
                    Debug.WriteLine("Error: AppWindow is null and could not be resolved from XamlRoot.");
                }
            }
        }
        #endregion

        #region Purge Page
        public Task Purge()
        {
            Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and ACL Collections...");

                _ = Task.Run(async () =>
                {
                    await Task.Delay(350);

                    var tcs = new TaskCompletionSource();
                    DispatcherQueue?.TryEnqueue(() =>
                    {
                        if (this.DataContext is IDisposable disposableVm) disposableVm.Dispose();

                        if (ViewModel != null)
                        {
                            if (ViewModel is IDisposable dispVM) dispVM.Dispose();
                            ViewModel = null!;
                        }

                        AppWindow = null!; // Added !

                        // this.Bindings?.StopTracking();
                        this.DataContext = null!;
                        this.Content = null!;

                        tcs.SetResult();
                    });

                    await tcs.Task;

                    DiagnosticsPageViewModel.Current?.ForceImmediateMemoryCleanup();

                    App.MemoryGuardian?.ForcePageTransitionCleanup();
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
