// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Core.Constants;
using EvolveOS_Optimizer.Utilities.Helpers;
using System.Threading;
using Microsoft.UI.Dispatching;

namespace EvolveOS_Optimizer.Utilities.Services;

public class DialogService : IDialogService
{
    #region Fields & Constructor

    private readonly ILocalizationService _localization;
    private readonly ILogService _logService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly SemaphoreSlim _dialogSemaphore = new(1, 1);

    public XamlRoot? XamlRoot { get; set; }

    public DialogService(ILocalizationService localization, ILogService logService, ITaskProgressService taskProgressService)
    {
        _localization = localization;
        _logService = logService;
        _taskProgressService = taskProgressService;
    }

    #endregion

    #region Base Configuration

    private void ConfigureDialog(ContentDialog dialog)
    {
        dialog.XamlRoot = XamlRoot;

        if (XamlRoot?.Content is FrameworkElement rootElement)
        {
            dialog.RequestedTheme = rootElement.ActualTheme == ElementTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        }

        var baseColor = dialog.RequestedTheme == ElementTheme.Dark ? Color.FromArgb(255, 44, 44, 44) : Color.FromArgb(255, 243, 243, 243);
        dialog.Background = new AcrylicBrush
        {
            TintColor = baseColor,
            TintOpacity = 0.65,
            TintLuminosityOpacity = 0.75,
            FallbackColor = baseColor
        };
    }

    #endregion

    #region Guard Helpers

    private async Task<T> ExecuteDialogAsync<T>(Func<Task<T>> dialogAction, T defaultValue)
    {
        await _dialogSemaphore.WaitAsync();
        try
        {
            if (XamlRoot == null && App.MainWindow?.Content != null)
            {
                XamlRoot = App.MainWindow.Content.XamlRoot;
            }

            if (XamlRoot == null)
            {
                _logService.LogWarning("[DialogService] XamlRoot is null");
                return defaultValue;
            }
            return await dialogAction();
        }
        finally
        {
            _dialogSemaphore.Release();
        }
    }

    private async Task ExecuteDialogAsync(Func<Task> dialogAction)
    {
        await ExecuteDialogAsync(async () => { await dialogAction(); return true; }, true);
    }

    #endregion

    #region Simple Dialogs

    private async Task ShowSimpleDialogAsync(string message, string title, string buttonText)
    {
        await ExecuteDialogAsync(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = buttonText,
                DefaultButton = ContentDialogButton.Close
            };
            ConfigureDialog(dialog);
            await dialog.ShowAsync();
        });
    }

    public void ShowMessage(string message, string title = "")
    {
        _ = ShowInformationAsync(message, title);
    }

    public async Task ShowInformationAsync(string message, string title = "Information", string buttonText = "OK")
        => await ShowSimpleDialogAsync(message, title, buttonText);

    public async Task ShowWarningAsync(string message, string title = "Warning", string buttonText = "OK")
        => await ShowSimpleDialogAsync(message, title, buttonText);

    public async Task ShowErrorAsync(string message, string title = "Error", string buttonText = "OK")
        => await ShowSimpleDialogAsync(message, title, buttonText);

    #endregion

    #region Confirmation Dialogs

    public async Task<bool> ShowConfirmationAsync(
        string message,
        string title = "",
        string okButtonText = "OK",
        string cancelButtonText = "Cancel")
    {
        return await ExecuteDialogAsync(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = string.IsNullOrEmpty(title) ? _localization.GetString("Dialog_Confirmation") : title,
                Content = message,
                PrimaryButtonText = okButtonText,
                CloseButtonText = cancelButtonText,
                DefaultButton = ContentDialogButton.Primary
            };
            ConfigureDialog(dialog);

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }, false);
    }

    public async Task<(bool Confirmed, bool CheckboxChecked)> ShowConfirmationWithCheckboxAsync(
        string message,
        string? checkboxText = null,
        string title = "Confirmation",
        string continueButtonText = "Continue",
        string cancelButtonText = "Cancel")
    {
        return await ExecuteDialogAsync(async () =>
        {

            var checkBox = new CheckBox { Content = checkboxText ?? "Don't show again", IsChecked = true };

            var contentPanel = new StackPanel { Spacing = 12 };
            contentPanel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(checkboxText))
            {
                contentPanel.Children.Add(checkBox);
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = contentPanel,
                PrimaryButtonText = continueButtonText,
                CloseButtonText = cancelButtonText,
                DefaultButton = ContentDialogButton.Primary
            };
            ConfigureDialog(dialog);

            var result = await dialog.ShowAsync();
            return (result == ContentDialogResult.Primary, checkBox.IsChecked == true);
        }, (false, false));
    }

    public async Task<(bool Confirmed, bool CheckboxChecked)> ShowAppOperationConfirmationAsync(
        string operationType,
        IEnumerable<string> itemNames,
        int count,
        string? checkboxText = null)
    {
        return await ExecuteDialogAsync(async () =>
        {
            bool isInstall = operationType.Equals("install", StringComparison.OrdinalIgnoreCase);
            bool isRemove = operationType.Equals("remove", StringComparison.OrdinalIgnoreCase);

            string title = isInstall ? _localization.GetString("Dialog_ConfirmInstallation") :
                          isRemove ? _localization.GetString("Dialog_ConfirmRemoval") :
                          _localization.GetString("Dialog_ConfirmOperation", operationType);

            string headerMessage = isInstall ? _localization.GetString("Dialog_ItemsWillBeInstalled") :
                                  isRemove ? _localization.GetString("Dialog_ItemsWillBeRemoved") :
                                  _localization.GetString("Dialog_ItemsWillBeProcessed", operationType.ToLower());

            var itemContainerStyle = new Style(typeof(ListViewItem));
            itemContainerStyle.Setters.Add(new Setter(ListViewItem.PaddingProperty, new Thickness(0)));
            itemContainerStyle.Setters.Add(new Setter(ListViewItem.MinHeightProperty, 0d));

            var listView = new ListView
            {
                ItemsSource = itemNames.ToList(),
                MaxHeight = 300,
                SelectionMode = ListViewSelectionMode.None,
                ItemContainerStyle = itemContainerStyle
            };

            var listContainer = new Border
            {
                Child = listView,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(
                    (XamlRoot?.Content as FrameworkElement)?.ActualTheme == ElementTheme.Dark
                        ? Colors.White : Colors.Black),
                BorderThickness = new Thickness(1)
            };

            var contentPanel = new StackPanel { Spacing = 12 };
            contentPanel.Children.Add(new TextBlock { Text = headerMessage, TextWrapping = TextWrapping.Wrap });
            contentPanel.Children.Add(listContainer);

            CheckBox? checkBox = null;
            if (!string.IsNullOrEmpty(checkboxText))
            {
                checkBox = new CheckBox { Content = checkboxText, IsChecked = true };

                checkBox.Checked += (_, _) => DialogAccessibilityHelper.AnnounceToNarrator(
                    checkBox,
                    $"{checkboxText}: {_localization.GetString("Accessibility_Checked") ?? "Checked"}",
                    "CheckboxStateChange");
                checkBox.Unchecked += (_, _) => DialogAccessibilityHelper.AnnounceToNarrator(
                    checkBox,
                    $"{checkboxText}: {_localization.GetString("Accessibility_Unchecked") ?? "Unchecked"}",
                    "CheckboxStateChange");

                contentPanel.Children.Add(checkBox);
            }

            var dialog = new ContentDialog
            {
                Title = title,
                Content = contentPanel,
                PrimaryButtonText = _localization.GetString("Button_Continue"),
                CloseButtonText = _localization.GetString("Button_Cancel"),
                DefaultButton = ContentDialogButton.Primary
            };
            ConfigureDialog(dialog);

            var result = await dialog.ShowAsync();
            return (result == ContentDialogResult.Primary, checkBox?.IsChecked == true);
        }, (false, false));
    }

    public async Task<ConfirmationResponse> ShowConfirmationAsync(
        ConfirmationRequest confirmationRequest,
        string continueButtonText = "Continue",
        string cancelButtonText = "Cancel")
    {
        return await ExecuteDialogAsync(async () =>
        {
            var contentPanel = new StackPanel { Spacing = 8 };

            if (!string.IsNullOrEmpty(confirmationRequest.Message))
            {
                contentPanel.Children.Add(new TextBlock
                {
                    Text = confirmationRequest.Message,
                    TextWrapping = TextWrapping.Wrap
                });
            }

            CheckBox? checkBox = null;
            if (!string.IsNullOrEmpty(confirmationRequest.CheckboxText))
            {
                checkBox = new CheckBox { Content = confirmationRequest.CheckboxText };
                contentPanel.Children.Add(checkBox);
            }

            var dialog = new ContentDialog
            {
                Title = confirmationRequest.Title,
                Content = contentPanel,
                PrimaryButtonText = continueButtonText,
                CloseButtonText = cancelButtonText,
                DefaultButton = ContentDialogButton.Primary
            };
            ConfigureDialog(dialog);

            var result = await dialog.ShowAsync();
            return new ConfirmationResponse
            {
                Confirmed = result == ContentDialogResult.Primary,
                CheckboxChecked = checkBox?.IsChecked == true
            };
        }, new ConfirmationResponse { Confirmed = false });
    }

    public async Task ShowTaskOutputDialogAsync(string title, IReadOnlyList<string> logMessages)
    {
        await ExecuteDialogAsync(async () =>
        {
            var builder = new Dialogs.TaskOutputDialogBuilder(_localization, _taskProgressService);
            var dialog = builder.Build(XamlRoot!, title, logMessages);
            ConfigureDialog(dialog);
            builder.StartLiveUpdates(DispatcherQueue.GetForCurrentThread());
            try
            {
                await dialog.ShowAsync();
            }
            finally
            {
                builder.StopLiveUpdates();
            }
        });
    }

    public async Task ShowCustomContentDialogAsync(string title, object content, string closeButtonText = "Close")
    {
        await ExecuteDialogAsync(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = closeButtonText,
                DefaultButton = ContentDialogButton.Close
            };
            ConfigureDialog(dialog);
            await dialog.ShowAsync();
        });
    }
    #endregion
}
