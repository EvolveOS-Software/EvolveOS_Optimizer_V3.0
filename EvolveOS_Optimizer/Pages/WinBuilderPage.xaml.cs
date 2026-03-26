// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.IO;
using Windows.System;
using Windows.Storage.Pickers;
using EvolveOS_Optimizer.Utilities.WinBuilder;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class WinBuilderPage : Page
    {
        public ObservableCollection<RegistryTweak> AvailableTweaks { get; set; } = new();
        public ObservableCollection<RemovableApp> AvailableApps { get; set; } = new();
        public ObservableCollection<ServiceTweak> AvailableServices { get; set; } = new();
        public ObservableCollection<RemovableElement> AvailableElements { get; set; } = new();

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
            picker.SuggestedFileName = "EvolveOS_Custom_Win11";
            picker.FileTypeChoices.Add("ISO Image", new[] { ".iso" });

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                TxtOutputIso.Text = file.Path;
            }
        }

        private async void BuildIso_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtSourceIso.Text) || string.IsNullOrWhiteSpace(TxtOutputIso.Text))
            {
                TxtStatus.Text = "Please select a source ISO and a save destination first.";
                return;
            }

            var selectedApps = AvailableApps.Where(a => a.IsSelected).Select(a => a.PackageName).ToList();
            var selectedTweaks = AvailableTweaks.Where(t => t.IsSelected).ToList();
            var selectedServices = AvailableServices.Where(s => s.IsSelected).ToList();
            var selectedElements = AvailableElements.Where(el => el.IsSelected).Select(el => el.PackageName).ToList();

            // Extract the string values from the new ComboBoxes
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

            BtnBuildIso.IsEnabled = false;
            BuildProgress.IsIndeterminate = true;

            var progressReporter = new Progress<string>(status =>
            {
                TxtStatus.Text = status;
            });

            var builderService = new IsoBuilderService();

            try
            {
                await builderService.BuildCustomIsoAsync(buildOptions, progressReporter);

                BuildProgress.IsIndeterminate = false;
                BuildProgress.Value = 100;
                TxtStatus.Text = "Success! Custom EvolveOS ISO created.";
            }
            catch (Exception ex)
            {
                BuildProgress.IsIndeterminate = false;
                TxtStatus.Text = $"Error: {ex.Message}";
            }
            finally
            {
                BtnBuildIso.IsEnabled = true;
            }
        }
    }
}