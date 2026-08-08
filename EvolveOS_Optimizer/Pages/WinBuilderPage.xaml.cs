// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using EvolveOS_Optimizer.Utilities.Services;
using EvolveOS_Optimizer.Utilities.WinBuilder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Markup;
using Windows.System;

namespace EvolveOS_Optimizer.Pages
{
    public sealed partial class WinBuilderPage : Page, IPurgeable
    {
        #region Collections
        public ObservableCollection<RemovableApp> AvailableApps { get; set; } = new();
        public ObservableCollection<RemovableElement> AvailableElements { get; set; } = new();
        #endregion

        #region State Fields
        private bool _isBuildInProgress = false;
        private CancellationTokenSource? _buildCts;

        private int _currentStep = 0;
        private const int MAX_STEPS = 5;

        private bool _isIsoMode = true;
        private List<RegistryTweak> _incomingTweaks = new();
        #endregion

        #region Constructor
        public WinBuilderPage()
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

            LoadCatalog();

            this.Unloaded += WinBuilderPage_Unloaded;
        }
        #endregion

        #region Wizard Navigation
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is WizardConfig config)
            {
                _incomingTweaks = config.Tweaks;
                _isIsoMode = config.Mode == "ISO";

                if (!_isIsoMode)
                {
                    _currentStep = 1;

                    GridTargetEdition.Visibility = Visibility.Collapsed;
                    GridImageFormat.Visibility = Visibility.Collapsed;
                    SeparatorIsoSettings.Visibility = Visibility.Collapsed;
                    GridRemoveWinRE.Visibility = Visibility.Collapsed;

                    WinBuild_Title.Text = ResourceString.GetString("autounattend_builder_title") ?? "Autounattend Builder";
                    WinBuild_Desc.Text = ResourceString.GetString("autounattend_builder_desc") ?? "Generate a custom unattended XML file for live Windows deployments.";

                    if (Application.Current.Resources.TryGetValue("AutounattendXmlIconPath", out object xmlIcon) && xmlIcon is string xmlString)
                    {
                        HeaderLogo.Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), xmlString);
                    }

                    UpdateWizardUI();
                }
                else
                {
                    GridTargetEdition.Visibility = Visibility.Visible;
                    GridImageFormat.Visibility = Visibility.Visible;
                    SeparatorIsoSettings.Visibility = Visibility.Visible;
                    GridRemoveWinRE.Visibility = Visibility.Visible;

                    WinBuild_Title.Text = ResourceString.GetString("winbuilder_title") ?? "Windows ISO Builder";
                    WinBuild_Desc.Text = ResourceString.GetString("winbuilder_description") ?? "Configure and build a custom Windows installation image.";

                    if (Application.Current.Resources.TryGetValue("WinBuilderIconPath", out object isoIcon) && isoIcon is string isoString)
                    {
                        HeaderLogo.Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), isoString);
                    }
                }
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == MAX_STEPS - 1)
            {
                BuildIso_Click(sender, e);
            }
            else if (_currentStep < MAX_STEPS - 1)
            {
                _currentStep++;
                UpdateWizardUI();
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 0)
            {
                if (!_isIsoMode && _currentStep == 2) _currentStep = 1;
                else _currentStep--;

                UpdateWizardUI();
            }
        }

        private void UpdateWizardUI()
        {
            // Visibility Toggles
            Step1_Source.Visibility = (_currentStep == 0) ? Visibility.Visible : Visibility.Collapsed;
            Step2_Rules.Visibility = (_currentStep == 1) ? Visibility.Visible : Visibility.Collapsed;
            Step3_Apps.Visibility = (_currentStep == 2) ? Visibility.Visible : Visibility.Collapsed;
            Step4_Features.Visibility = (_currentStep == 3) ? Visibility.Visible : Visibility.Collapsed;
            Step5_Execution.Visibility = (_currentStep == 4) ? Visibility.Visible : Visibility.Collapsed;

            // Navigation Buttons
            BtnBack.IsEnabled = (_currentStep > 0);
            BtnNext.Content = (_currentStep == MAX_STEPS - 1) ? "Finish" : "Next";

            // Step Indicator Update
            if (TxtStepIndicator != null)
            {
                TxtStepIndicator.Text = $"Step {_currentStep + 1} of {MAX_STEPS}";
            }

            // Dynamic Button Text for Step 5
            if (_currentStep == MAX_STEPS - 1)
            {
                BtnBuildIso.Content = _isIsoMode ? "Begin ISO Build" : "Generate XML File";
            }
        }
        #endregion

        #region Lifecycle & Catalog
        private void WinBuilderPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _ = Purge();
        }

        private void LoadCatalog()
        {
            var apps = EvolveOSCatalog.GetAvailableApps();
            foreach (var app in apps)
            {
                app.IsSelected = true;
                AvailableApps.Add(app);
            }

            var elements = EvolveOSCatalog.GetAvailableElements();
            foreach (var element in elements)
            {
                element.IsSelected = true;
                AvailableElements.Add(element);
            }
        }
        #endregion

        #region UI Handlers
        private async void DownloadWin11_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.microsoft.com/software-download/windows11"));
        }

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            var window = App.MainWindow;
            if (window == null) return;

            string title = "Select Source Windows ISO";
            string filterName = "ISO Image (*.iso)";
            string filterPattern = "*.iso";

            string? filePath = Win32FileDialogHelper.ShowOpenFilePicker(
                window,
                title,
                filterName,
                filterPattern);

            if (!string.IsNullOrEmpty(filePath))
            {
                TxtSourceIso.Text = filePath;
            }
        }

        private void BrowseDestination_Click(object sender, RoutedEventArgs e)
        {
            var window = App.MainWindow;
            if (window == null) return;

            string title = _isIsoMode ? "Save Custom ISO" : "Save Autounattend File";
            string filterName = _isIsoMode ? "ISO Image (*.iso)" : "XML File (*.xml)";
            string filterPattern = _isIsoMode ? "*.iso" : "*.xml";
            string defaultFileName = _isIsoMode ? "EvolveOS_Custom_Win11" : "autounattend";
            string defaultExtension = _isIsoMode ? "iso" : "xml";

            string? filePath = Win32FileDialogHelper.ShowSaveFilePicker(
                window,
                title,
                filterName,
                filterPattern,
                defaultFileName,
                defaultExtension);

            if (!string.IsNullOrEmpty(filePath))
            {
                TxtOutputIso.Text = filePath;
            }
        }
        #endregion

        #region Build Logic
        private async void BuildIso_Click(object sender, RoutedEventArgs e)
        {
            if (_isBuildInProgress)
            {
                BtnBuildIso.IsEnabled = false;
                BtnBuildIso.Content = ResourceString.GetString("winbuilder_btn_cancelling") ?? "Cancelling...";
                _buildCts?.Cancel();
                return;
            }

            if (_isIsoMode && string.IsNullOrWhiteSpace(TxtSourceIso.Text))
            {
                TxtStatus.Text = "Please select a source ISO first.";
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtOutputIso.Text))
            {
                TxtStatus.Text = "Please select a save destination first.";
                return;
            }

            var selectedApps = AvailableApps.Where(a => a.IsSelected).Select(a => a.PackageName).ToList();
            var selectedElements = AvailableElements.Where(el => el.IsSelected).Select(el => el.PackageName).ToList();

            // Use the _incomingTweaks from the ProfileBuilder.
            var selectedTweaks = _incomingTweaks;

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
                AppsToRemove = selectedApps ?? new(),
                RegistryTweaks = selectedTweaks ?? new(),
                ElementsToRemove = selectedElements ?? new(),

                ServiceTweaks = new()
            };

            BtnBuildIso.Content = "Cancel Build";
            BuildProgress.IsIndeterminate = true;
            _isBuildInProgress = true;
            _buildCts = new CancellationTokenSource();

            EfficiencyModeHelper.IsUIWakeLockActive = true;
            EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(false);

            IProgress<string> progressReporter = new Progress<string>(status => { if (TxtStatus != null) TxtStatus.Text = status; });

            try
            {
                if (_isIsoMode)
                {
                    var builderService = new IsoBuilderService();
                    await builderService.BuildCustomIsoAsync(buildOptions, progressReporter, _buildCts.Token);
                    TxtStatus.Text = "Success! Custom EvolveOS ISO created.";
                }
                else
                {
                    progressReporter.Report("Generating Autounattend.xml...");
                    var xmlService = new AutounattendBuilderService();
                    await xmlService.GenerateAsync(buildOptions);
                    TxtStatus.Text = "Success! Autounattend.xml saved.";
                }

                BuildProgress.IsIndeterminate = false;
                BuildProgress.Value = 100;

                BuildControlsGrid.Visibility = Visibility.Collapsed;
                PostBuildPanel.Visibility = Visibility.Visible;
                FooterGrid.Visibility = Visibility.Collapsed;
                BtnBack.Visibility = Visibility.Collapsed;
                BtnNext.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException)
            {
                BuildProgress.IsIndeterminate = false;
                BuildProgress.Value = 0;
                TxtStatus.Text = "Operation was cancelled.";
            }
            catch (Exception ex)
            {
                BuildProgress.IsIndeterminate = false;
                TxtStatus.Text = $"Error: {ex.Message}";
            }
            finally
            {
                _isBuildInProgress = false;
                BtnBuildIso.IsEnabled = true;
                BtnBuildIso.Content = "Build";
                _buildCts?.Dispose();
                _buildCts = null;
                EfficiencyModeHelper.IsUIWakeLockActive = false;
                if (LocalMachineSettingsEngine.RunOnPriority == Core.Enums.Priority.Low)
                {
                    EfficiencyModeHelper.SetCurrentProcessEfficiencyMode(true);
                }
            }
        }
        #endregion

        #region Post-Build Navigation

        private void ReturnToProfileBuilder()
        {
            try
            {
                if (App.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.NavigateByTag("ProfileBuilder");
                    return;
                }

                if (this.Frame != null)
                {
                    this.Frame.Navigate(typeof(ProfileBuilderPage));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Navigation Error] Failed to return: {ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ReturnToProfileBuilder();
        }

        private void ReturnKeep_Click(object sender, RoutedEventArgs e)
        {
            ReturnToProfileBuilder();
        }

        private void ReturnReset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var profileVm = App.Services.GetRequiredService<ProfileBuilderViewModel>();

                profileVm?.ClearTempState();
                profileVm?.PurgeProfile();
                profileVm?.IsDirty = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Purge Error]: {ex.Message}");
            }
            finally
            {
                ReturnToProfileBuilder();
            }
        }

        #endregion

        #region Navigation Handling

        private bool _isConfirmedNavigation = false;
        private bool _isDialogShowing = false;

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (_isConfirmedNavigation)
            {
                base.OnNavigatingFrom(e);
                return;
            }

            if (_isDialogShowing)
            {
                e.Cancel = true;
                return;
            }

            if (e.SourcePageType == typeof(ProfileBuilderPage))
            {
                base.OnNavigatingFrom(e);
                return;
            }

            if (PostBuildPanel.Visibility == Visibility.Visible)
            {
                _isConfirmedNavigation = true;
                var profileVm = App.Services.GetRequiredService<ProfileBuilderViewModel>();
                if (profileVm != null) { profileVm.ClearTempState(); profileVm.PurgeProfile(); profileVm.IsDirty = false; }
                base.OnNavigatingFrom(e);
                return;
            }

            var vm = App.Services.GetRequiredService<ProfileBuilderViewModel>();

            bool hasProfileTweaks = vm != null && vm.HasUnsavedChanges();

            bool hasWinBuilderInput = !string.IsNullOrWhiteSpace(TxtOutputIso.Text)
                                   || !string.IsNullOrWhiteSpace(TxtSourceIso.Text)
                                   || (_isIsoMode ? _currentStep > 0 : _currentStep > 1);

            if (!_isBuildInProgress && !hasProfileTweaks && !hasWinBuilderInput)
            {
                _isConfirmedNavigation = true;
                if (vm != null) { vm.ClearTempState(); vm.PurgeProfile(); vm.IsDirty = false; }
                base.OnNavigatingFrom(e);
                return;
            }

            e.Cancel = true;
            _ = HandleNavigationConfirmationAsync(e);
        }

        private async Task HandleNavigationConfirmationAsync(NavigatingCancelEventArgs e)
        {
            _isDialogShowing = true;

            string title = _isBuildInProgress ? "Build in Progress" : "Abort Build Setup?";
            string content = _isBuildInProgress
                ? "A deployment operation is currently running. Are you sure you want to cancel the build and leave?"
                : "You are currently in the middle of the deployment setup. If you leave now, your settings will be reset. Are you sure you want to abort and leave?";
            string primaryText = _isBuildInProgress ? "Cancel Build & Leave" : "Abort & Reset";

            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primaryText,
                CloseButtonText = "Stay Here",
                XamlRoot = this.XamlRoot,
                RequestedTheme = this.ActualTheme
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (_buildCts != null && !_buildCts.IsCancellationRequested) _buildCts.Cancel();
                _isBuildInProgress = false;

                var profileVm = App.Services.GetRequiredService<ProfileBuilderViewModel>();
                if (profileVm != null) { profileVm.ClearTempState(); profileVm.PurgeProfile(); profileVm.IsDirty = false; }

                this.NavigationCacheMode = NavigationCacheMode.Disabled;

                _isConfirmedNavigation = true;

                if (e.NavigationMode == NavigationMode.Back) Frame.GoBack();
                else Frame.Navigate(e.SourcePageType, e.Parameter, e.NavigationTransitionInfo);
            }
            else
            {
                _isConfirmedNavigation = false;

                if (App.MainWindow is MainWindow mainWindow) mainWindow.SwitchPage("ProfileBuilder");
            }

            _isDialogShowing = false; // Unlock
        }

        #endregion

        #region Purge Page
        public async Task Purge()
        {
            Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

            if (_buildCts != null)
            {
                try { _buildCts.Cancel(); _buildCts.Dispose(); } catch { }
                _buildCts = null;
            }

            if (!SettingsEngine.IsHighPerformanceModeEnabled)
            {
                Debug.WriteLine($"[{this.GetType().Name}] Low Resource Mode: Nuking UI and Collections...");

                this.Unloaded -= WinBuilderPage_Unloaded;

                _ = Task.Run(async () =>
                {
                    await Task.Delay(350);

                    DispatcherQueue?.TryEnqueue(() =>
                    {
                        AvailableApps?.Clear();
                        AvailableElements?.Clear();

                        if (this.FindName("AppsItemsControl") is ItemsControl appsControl)
                            appsControl.ItemsSource = null;

                        if (this.FindName("ElementsItemsControl") is ItemsControl elementsControl)
                            elementsControl.ItemsSource = null;

                        this.Bindings?.StopTracking();
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
        }
        #endregion
    }
}