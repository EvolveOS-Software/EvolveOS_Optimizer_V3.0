// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.WinBuilder;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using Windows.System;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class WinBuilderPage : Page
    {
        public ObservableCollection<RegistryTweak> AvailableTweaks { get; set; } = new();
        public ObservableCollection<RemovableApp> AvailableApps { get; set; } = new();
        public ObservableCollection<ServiceTweak> AvailableServices { get; set; } = new();
        public ObservableCollection<RemovableElement> AvailableElements { get; set; } = new();

        private bool _isBuildInProgress = false;
        private bool _isDialogShowing = false;
        private CancellationTokenSource? _buildCts;

        public WinBuilderPage()
        {
            this.InitializeComponent();
            LoadCatalog();
        }

        private void LoadCatalog()
        {
            var tweaks = EvolveOSCatalog.GetAvailableTweaks();
            foreach (var tweak in tweaks)
            {
                tweak.IsSelected = true;
                AvailableTweaks.Add(tweak);
            }
            TweaksItemsControl.ItemsSource = AvailableTweaks;

            var apps = EvolveOSCatalog.GetAvailableApps();
            foreach (var app in apps)
            {
                app.IsSelected = true;
                AvailableApps.Add(app);
            }
            AppsItemsControl.ItemsSource = AvailableApps;

            var services = EvolveOSCatalog.GetAvailableServices();
            foreach (var service in services)
            {
                service.IsSelected = true;
                AvailableServices.Add(service);
            }
            ServicesItemsControl.ItemsSource = AvailableServices;

            var elements = EvolveOSCatalog.GetAvailableElements();
            foreach (var element in elements)
            {
                element.IsSelected = true;
                AvailableElements.Add(element);
            }
            ElementsItemsControl.ItemsSource = AvailableElements;
        }

        private async void DownloadWin11_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.microsoft.com/software-download/windows11"));
        }

        private async void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();

            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.Downloads;
            picker.FileTypeFilter.Add(".iso");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                TxtSourceIso.Text = file.Path;
            }
        }

        private async void BrowseDestination_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker();

            var window = App.MainWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.SuggestedFileName = ResourceString.GetString("winbuilder_default_iso_name") ?? "EvolveOS_Custom_Win11";

            string filterName = ResourceString.GetString("winbuilder_iso_image_filter") ?? "ISO Image";
            picker.FileTypeChoices.Add(filterName, new[] { ".iso" });

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                TxtOutputIso.Text = file.Path;
            }
        }

        private async void BuildIso_Click(object sender, RoutedEventArgs e)
        {
            if (_isBuildInProgress)
            {
                BtnBuildIso.IsEnabled = false;
                BtnBuildIso.Content = ResourceString.GetString("winbuilder_btn_cancelling") ?? "Cancelling...";
                _buildCts?.Cancel();
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtSourceIso.Text) || string.IsNullOrWhiteSpace(TxtOutputIso.Text))
            {
                TxtStatus.Text = ResourceString.GetString("winbuilder_err_select_paths") ?? "Please select a source ISO and a save destination first.";
                return;
            }

            var selectedApps = AvailableApps.Where(a => a.IsSelected).Select(a => a.PackageName).ToList();
            var selectedTweaks = AvailableTweaks.Where(t => t.IsSelected).ToList();
            var selectedServices = AvailableServices.Where(s => s.IsSelected).ToList();
            var selectedElements = AvailableElements.Where(el => el.IsSelected).Select(el => el.PackageName).ToList();

            string targetEdition = (CmbTargetEdition.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Pro";
            string imageFormat = (CmbImageFormat.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "WIM";

            var buildOptions = new IsoBuildOptions
            {
                SourceIsoPath = TxtSourceIso.Text,
                OutputIsoPath = TxtOutputIso.Text,
                WorkingDirectory = Path.Combine(Path.GetTempPath(), "EvolveOS_ISO_Builder"),

                TargetEdition = targetEdition,
                ImageFormat = imageFormat,

                BypassWin11Requirements = ToggleBypassReqs.IsOn,
                BypassMicrosoftAccount = ToggleBypassMSA.IsOn,
                EnableNet35 = ToggleNet35.IsOn,

                RemoveWindowsRecovery = ToggleRemoveWinRE.IsOn,

                RemoveMicrosoftEdge = ChkRemoveEdge.IsChecked ?? false,
                RemoveOneDrive = ChkRemoveOneDrive.IsChecked ?? false,

                AppsToRemove = selectedApps,
                RegistryTweaks = selectedTweaks,
                ServiceTweaks = selectedServices,
                ElementsToRemove = selectedElements
            };

            BtnBuildIso.Content = ResourceString.GetString("winbuilder_btn_cancel_build") ?? "Cancel Build";

            BuildProgress.IsIndeterminate = true;
            _isBuildInProgress = true;
            _buildCts = new CancellationTokenSource();

            EfficiencyModeHelper.IsUIWakeLockActive = true;
            EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

            var progressReporter = new Progress<string>(status =>
            {
                TxtStatus.Text = status;
            });

            var builderService = new IsoBuilderService();

            try
            {
                await builderService.BuildCustomIsoAsync(buildOptions, progressReporter, _buildCts.Token);

                BuildProgress.IsIndeterminate = false;
                BuildProgress.Value = 100;
                TxtStatus.Text = ResourceString.GetString("winbuilder_msg_success") ?? "Success! Custom EvolveOS ISO created.";
            }
            catch (OperationCanceledException)
            {
                BuildProgress.IsIndeterminate = false;
                BuildProgress.Value = 0;
                TxtStatus.Text = ResourceString.GetString("winbuilder_msg_cancelled") ?? "Build was successfully cancelled.";
            }
            catch (Exception ex)
            {
                BuildProgress.IsIndeterminate = false;
                string errorPrefix = ResourceString.GetString("winbuilder_msg_error_prefix") ?? "Error:";
                TxtStatus.Text = $"{errorPrefix} {ex.Message}";
            }
            finally
            {
                _isBuildInProgress = false;
                BtnBuildIso.IsEnabled = true;
                BtnBuildIso.Content = ResourceString.GetString("winbuilder_btn_build") ?? "Build Custom ISO";

                _buildCts?.Dispose();
                _buildCts = null;

                EfficiencyModeHelper.IsUIWakeLockActive = false;
                if (LocalMachineSettingsEngine.RunOnPriority == Enums.Priority.Low)
                {
                    EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);
                }
            }
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (!_isBuildInProgress)
            {
                base.OnNavigatingFrom(e);
                return;
            }

            e.Cancel = true;

            if (_isDialogShowing) return;
            _isDialogShowing = true;

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("winbuilder_dialog_title") ?? "Build in Progress",
                Content = ResourceString.GetString("winbuilder_dialog_content") ?? "An ISO build is currently running. If you leave this page, the build will be permanently cancelled. What would you like to do?",
                PrimaryButtonText = ResourceString.GetString("winbuilder_dialog_btn_leave") ?? "Cancel Build & Leave",
                CloseButtonText = ResourceString.GetString("winbuilder_dialog_btn_wait") ?? "Wait",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            _isDialogShowing = false;

            if (result == ContentDialogResult.Primary)
            {
                _buildCts?.Cancel();
                _isBuildInProgress = false;

                Frame.Navigate(e.SourcePageType, e.Parameter, e.NavigationTransitionInfo);
            }
        }
    }
}