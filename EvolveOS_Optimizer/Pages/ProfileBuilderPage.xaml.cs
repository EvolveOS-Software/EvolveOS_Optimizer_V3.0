// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.ViewModel;
using EvolveOS_Optimizer.Core.ViewModel.Builder;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace EvolveOS_Optimizer.Pages;

public sealed partial class ProfileBuilderPage : Page
{
    #region Properties

    public ProfileBuilderViewModel ViewModel { get; }

    #endregion

    #region Constructor

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

    private async void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var windowProvider = App.Services.GetRequiredService<IMainWindowProvider>();
        var window = windowProvider.MainWindow;

        if (window == null) return;

        string title = "Import EvolveOS Profile";
        string filterName = "EvolveOS Profile (*.json)";
        string filterPattern = "*.json";

        // 1. Pick the file
        string? filePath = Win32FileDialogHelper.ShowOpenFilePicker(
            window,
            title,
            filterName,
            filterPattern);

        if (!string.IsNullOrEmpty(filePath))
        {
            // 2. Build the custom dialog UI dynamically
            var applyImmediatelyCheckBox = new CheckBox
            {
                Content = "Apply profile settings immediately (Skip preview)",
                Margin = new Thickness(0, 12, 0, 0),
                IsChecked = false // Default to Preview Mode (staged)
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

            // 3. Show the dialog and wait for the user's choice
            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                bool applyImmediately = applyImmediatelyCheckBox.IsChecked == true;

                try
                {
                    // 4. Pass BOTH the file path and the user's choice to the ViewModel
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
            Title = "Purge Profile?",
            Content = "Are you sure you want to clear all settings? This will reset the builder to its default state and cannot be undone.",
            PrimaryButtonText = "Purge",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            ViewModel.PurgeProfile();
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
        var targetCategory = ViewModel.Categories.FirstOrDefault(c => c.DisplayName == suggestion.SectionDisplayName);

        if (targetCategory != null)
        {
            ViewModel.SelectedCategory = targetCategory;
        }
    }

    #endregion

    #region Page Lifecycle & Purge

    protected override async void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        await Purge();
    }

    public async Task Purge()
    {
        Debug.WriteLine($"[{this.GetType().Name}] Purge requested...");

        if (!SettingsEngine.IsHighPerformanceModeEnabled)
        {
            await Task.Run(() =>
            {
                DiagnosticsPageViewModel.Current.ForceImmediateMemoryCleanup();
            });
        }
        else
        {
            Debug.WriteLine($"[{this.GetType().Name}] State preserved in RAM cache.");
        }
    }

    #endregion
}