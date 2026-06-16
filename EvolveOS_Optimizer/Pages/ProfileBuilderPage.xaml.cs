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

        string title = ResourceString.GetString("ProfileBuilder_ExportTitle");
        string filterName = ResourceString.GetString("ProfileBuilder_ExportFilterName");
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
                    Title = ResourceString.GetString("ProfileBuilder_ExportSuccessTitle"),
                    Content = ResourceString.GetString("ProfileBuilder_ExportSuccessContent"),
                    CloseButtonText = ResourceString.GetString("Global_OK"),
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = ResourceString.GetString("ProfileBuilder_ExportFailedTitle"),
                    Content = ResourceString.GetString("ProfileBuilder_ExportFailedContent", ex.Message),
                    CloseButtonText = ResourceString.GetString("Global_OK"),
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

        string title = ResourceString.GetString("ProfileBuilder_ImportTitle");
        string filterName = ResourceString.GetString("ProfileBuilder_ImportFilterName");
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
                Content = ResourceString.GetString("ProfileBuilder_ImportApplyImmediately"),
                Margin = new Thickness(0, 12, 0, 0),
                IsChecked = false
            };

            var dialogContent = new StackPanel();
            dialogContent.Children.Add(new TextBlock
            {
                Text = ResourceString.GetString("ProfileBuilder_ImportPromptContent"),
                TextWrapping = TextWrapping.Wrap
            });
            dialogContent.Children.Add(applyImmediatelyCheckBox);

            var dialog = new ContentDialog
            {
                Title = ResourceString.GetString("ProfileBuilder_ImportTitle"),
                Content = dialogContent,
                PrimaryButtonText = ResourceString.GetString("Global_Import"),
                CloseButtonText = ResourceString.GetString("Global_Cancel"),
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
                        Title = applyImmediately ? ResourceString.GetString("ProfileBuilder_ImportSuccessAppliedTitle") : ResourceString.GetString("ProfileBuilder_ImportSuccessStagedTitle"),
                        Content = applyImmediately
                            ? ResourceString.GetString("ProfileBuilder_ImportSuccessAppliedContent")
                            : ResourceString.GetString("ProfileBuilder_ImportSuccessStagedContent"),
                        CloseButtonText = ResourceString.GetString("Global_OK"),
                        XamlRoot = this.XamlRoot
                    };
                    await successDialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = ResourceString.GetString("ProfileBuilder_ImportFailedTitle"),
                        Content = ResourceString.GetString("ProfileBuilder_ImportFailedContent", ex.Message),
                        CloseButtonText = ResourceString.GetString("Global_OK"),
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
            Title = ResourceString.GetString("ProfileBuilder_ApplyRecommendedTitle"),
            Content = ResourceString.GetString("ProfileBuilder_ApplyRecommendedContent"),
            PrimaryButtonText = ResourceString.GetString("Global_OK"),
            CloseButtonText = ResourceString.GetString("Global_Cancel"),
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
            Title = ResourceString.GetString("ProfileBuilder_ResetDefaultsTitle"),
            Content = ResourceString.GetString("ProfileBuilder_ResetDefaultsContent"),
            PrimaryButtonText = ResourceString.GetString("Global_OK"),
            CloseButtonText = ResourceString.GetString("Global_Cancel"),
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
            Title = ResourceString.GetString("ProfileBuilder_PurgeTitle"),
            Content = ResourceString.GetString("ProfileBuilder_PurgeContent"),
            PrimaryButtonText = ResourceString.GetString("Global_Clear"),
            CloseButtonText = ResourceString.GetString("Global_Cancel"),
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
            Title = ResourceString.GetString("ProfileBuilder_ApplyPCTitle"),
            Content = ResourceString.GetString("ProfileBuilder_ApplyPCContent"),
            PrimaryButtonText = ResourceString.GetString("ProfileBuilder_ApplyNow"),
            CloseButtonText = ResourceString.GetString("Global_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.ApplyToLocalSystemCommand.ExecuteAsync(null);

            var successDialog = new ContentDialog
            {
                Title = ResourceString.GetString("ProfileBuilder_DeploymentCompleteTitle"),
                Content = ResourceString.GetString("ProfileBuilder_DeploymentCompleteContent"),
                CloseButtonText = ResourceString.GetString("Global_OK"),
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
            Title = ResourceString.GetString("ProfileBuilder_UnsavedTitle"),
            Content = ResourceString.GetString("ProfileBuilder_UnsavedContent"),
            PrimaryButtonText = ResourceString.GetString("ProfileBuilder_LeaveReset"),
            CloseButtonText = ResourceString.GetString("ProfileBuilder_StayHere"),
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