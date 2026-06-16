// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class ProfileBuilderPage : Page
{
    #region Properties

    public ProfileBuilderViewModel ViewModel { get; }

    #endregion

    #region Constructor & Life Cycle

    public ProfileBuilderPage()
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

        ViewModel = App.Services.GetRequiredService<ProfileBuilderViewModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        ViewModel.RestoreTempState();
        ViewModel.EvaluateExportState();
    }

    #endregion

    #region UI Event Handlers

    private async void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        var windowProvider = App.Services.GetRequiredService<IMainWindowProvider>();
        var window = windowProvider.MainWindow;

        if (window == null)
        {
            return;
        }

        string title = "Export EvolveOS Profile";
        string filterName = "EvolveOS Profile (*.json)";
        string filterPattern = "*.json";
        string defaultFileName = $"EvolveOS_Profile_{DateTime.Now:yyyyMMdd}";
        string defaultExtension = "json";

        string? filePath = Win32FileDialogHelper.ShowSaveFilePicker(
            window,
            title,
            filterName,
            filterPattern,
            defaultFileName,
            defaultExtension);

        if (!string.IsNullOrEmpty(filePath))
        {
            try
            {
                ViewModel.SaveProfile(filePath);

                var dialog = new ContentDialog
                {
                    Title = "Profile Exported",
                    Content = "Your configuration profile was saved successfully.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Export Failed",
                    Content = $"An error occurred while saving the profile: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
    }

    private void ExportXml_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveTempState();

        var config = new WizardConfig { Mode = "XML", Tweaks = ViewModel.GetSelectedTweaks() };
        Frame.Navigate(typeof(WinBuilderPage), config);
    }

    private void ExportIso_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveTempState();

        var config = new WizardConfig { Mode = "ISO", Tweaks = ViewModel.GetSelectedTweaks() };
        Frame.Navigate(typeof(WinBuilderPage), config);
    }

    private async void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var windowProvider = App.Services.GetRequiredService<IMainWindowProvider>();
        var window = windowProvider.MainWindow;

        if (window == null) return;

        string title = "Import EvolveOS Profile";
        string filterName = "EvolveOS Profile (*.json)";
        string filterPattern = "*.json";

        string? filePath = Win32FileDialogHelper.ShowOpenFilePicker(
            window,
            title,
            filterName,
            filterPattern);

        if (!string.IsNullOrEmpty(filePath))
        {
            var applyImmediatelyCheckBox = new CheckBox
            {
                Content = "Apply profile settings immediately (Skip preview)",
                Margin = new Thickness(0, 12, 0, 0),
                IsChecked = false
            };

            var dialogContent = new StackPanel();
            dialogContent.Children.Add(new TextBlock
            {
                Text = "How would you like to load this profile?",
                TextWrapping = TextWrapping.Wrap
            });
            dialogContent.Children.Add(applyImmediatelyCheckBox);

            var dialog = new ContentDialog
            {
                Title = "Import Profile",
                Content = dialogContent,
                PrimaryButtonText = "Import",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                bool applyImmediately = applyImmediatelyCheckBox.IsChecked == true;

                try
                {
                    ViewModel.LoadProfile(filePath, applyImmediately);

                    var successDialog = new ContentDialog
                    {
                        Title = applyImmediately ? "Profile Applied" : "Profile Staged",
                        Content = applyImmediately
                            ? "Your configuration profile was loaded and immediately applied to the builder."
                            : "Your configuration profile was loaded in Preview Mode. You can now review and accept the proposed changes.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await successDialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "Import Failed",
                        Content = $"An error occurred while loading the profile: {ex.Message}\nMake sure it is a valid EvolveOS JSON profile.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }
    }

    private async void ApplyRecommended_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Apply Recommended",
            Content = "This will update all settings in the builder to their EvolveOS Optimizer Recommended values. Continue?",
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.ApplyAllRecommended();
        }
    }

    private async void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Reset to Defaults",
            Content = "This will update all settings in the builder to the standard Windows Default values. Continue?",
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.ApplyAllDefaults();
        }
    }

    private async void PurgeProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Clear Profile?",
            Content = "Are you sure you want to clear all settings? This will reset the builder to its default state and cannot be undone.",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            ViewModel.IsPurging = true;

            await Task.Delay(50);

            try
            {
                ViewModel.PurgeProfile();
            }
            finally
            {
                ViewModel.IsPurging = false;
            }
        }
    }

    private async void ApplyToPC_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Apply Configuration to Local PC?",
            Content = "This will bypass the builder sandbox and immediately apply all configured settings, tweaks, and optimizations directly to this computer. Do you want to proceed?",
            PrimaryButtonText = "Apply Now",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.ApplyToLocalSystemCommand.ExecuteAsync(null);

            var successDialog = new ContentDialog
            {
                Title = "Deployment Complete",
                Content = "The profile configuration has been successfully applied to your local system.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await successDialog.ShowAsync();
        }
    }

    #endregion

    #region Search

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ViewModel.UpdateSearchSuggestions(sender.Text);
        }
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchSuggestionItem suggestion)
        {
            NavigateToSearchedSetting(suggestion);
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SearchSuggestionItem suggestion)
        {
            NavigateToSearchedSetting(suggestion);
        }
    }

    private void NavigateToSearchedSetting(SearchSuggestionItem suggestion)
    {
        var targetCategory = ViewModel.Categories.OfType<BuilderFeatureCategory>()
                                         .FirstOrDefault(c => c.DisplayName == suggestion.SectionDisplayName);

        if (targetCategory != null)
        {
            ViewModel.SelectedCategory = targetCategory;
        }
    }

    #endregion

    #region Page Lifecycle & Purge

    private bool _isConfirmedNavigation = false;
    private bool _isDialogShowing = false;

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        if (_isConfirmedNavigation || e.SourcePageType == typeof(WinBuilderPage))
        {
            base.OnNavigatingFrom(e);
            return;
        }

        if (_isDialogShowing)
        {
            e.Cancel = true;
            return;
        }

        if (!ViewModel.HasUnsavedChanges())
        {
            _isConfirmedNavigation = true;
            base.OnNavigatingFrom(e);
            return;
        }

        e.Cancel = true;
        _ = HandleNavigationConfirmationAsync(e);
    }

    private async Task HandleNavigationConfirmationAsync(NavigatingCancelEventArgs e)
    {
        _isDialogShowing = true;

        var dialog = new ContentDialog
        {
            Title = "Unsaved Configuration",
            Content = "You have an active configuration. If you leave now, your current settings will be reset. Are you sure you want to leave?",
            PrimaryButtonText = "Leave & Reset",
            CloseButtonText = "Stay Here",
            XamlRoot = this.XamlRoot,
            RequestedTheme = this.ActualTheme
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            ViewModel.ClearTempState();
            ViewModel.PurgeProfile();
            ViewModel.IsDirty = false;

            _isConfirmedNavigation = true;

            if (e.NavigationMode == NavigationMode.Back) Frame.GoBack();
            else Frame.Navigate(e.SourcePageType, e.Parameter, e.NavigationTransitionInfo);
        }
        else
        {
            _isConfirmedNavigation = false;

            var windowProvider = App.Services.GetRequiredService<IMainWindowProvider>();
            if (windowProvider.MainWindow is MainWindow mainWindow) mainWindow.SwitchPage("ProfileBuilder");
        }

        _isDialogShowing = false;
    }

    protected override async void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _isConfirmedNavigation = false;
        await Purge();
    }

    public async Task Purge()
    {
        Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

        if (!SettingsEngine.IsHighPerformanceModeEnabled)
        {
            await Task.Run(() => { DiagnosticsPageViewModel.Current.ForceImmediateMemoryCleanup(); });
        }
    }

    #endregion
}