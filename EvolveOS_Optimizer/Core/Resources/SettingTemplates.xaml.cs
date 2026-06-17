// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Core.Resources;

public sealed partial class SettingTemplates : ResourceDictionary
{
    public SettingTemplates()
    {
        this.InitializeComponent();
    }

    private async void RestartNow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button clickedButton && clickedButton.XamlRoot != null)
        {
            await ShowRestartConfirmationAsync(clickedButton.XamlRoot);
        }
    }

    private async Task ShowRestartConfirmationAsync(XamlRoot root)
    {
        var dialog = new ContentDialog
        {
            Title = ResourceString.GetString("RestartRequired_DialogTitle"),
            Content = ResourceString.GetString("RestartRequired_DialogContent"),
            PrimaryButtonText = ResourceString.GetString("RestartRequired_PrimaryButton"),
            CloseButtonText = ResourceString.GetString("RestartRequired_CloseButton"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = root
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            System.Diagnostics.Process.Start("shutdown", "/r /f /t 0");
        }
    }
}
