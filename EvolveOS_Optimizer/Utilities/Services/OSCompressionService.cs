// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Interfaces;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.Services;

public class OSCompressionService : IActionCommandProvider
{
    private readonly ILogService _logService;

    public IReadOnlySet<string> SupportedCommands { get; } = new HashSet<string> { "gaming-performance-os-compression" };

    public OSCompressionService(ILogService logService)
    {
        _logService = logService;
    }

    public async Task ExecuteCommandAsync(string command)
    {
        if (command != "gaming-performance-os-compression") return;

        try
        {
            _logService.Log(LogLevel.Info, "[OSCompression] Querying current state...");

            var xamlRoot = App.MainWindow?.Content?.XamlRoot;
            if (xamlRoot == null)
            {
                _logService.Log(LogLevel.Error, "[OSCompression] Could not find XamlRoot to display the dialog.");
                return;
            }

            var status = await CommandExecutor.StartTask("compact.exe /compactos:query");

            bool isCurrentlyCompressed = status.Contains("is in the Compact state", StringComparison.OrdinalIgnoreCase);

            var compressDialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                Title = ResourceString.GetString("OSCompression_Title"),
                Content = status,

                PrimaryButtonText = isCurrentlyCompressed
                    ? ResourceString.GetString("OSCompression_BtnDecompress")
                    : ResourceString.GetString("OSCompression_BtnCompress"),

                SecondaryButtonText = null,
                CloseButtonText = ResourceString.GetString("OSCompression_BtnCancel")
            };

            var result = await compressDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                if (isCurrentlyCompressed)
                {
                    App.ShowNotification(ResourceString.GetString("OSCompression_Title"), ResourceString.GetString("OSCompression_StatusDecompressing"), InfoBarSeverity.Informational, 5000);
                    var decompressResult = await CommandExecutor.StartTask("compact.exe /compactos:never");
                    App.ShowNotification(ResourceString.GetString("OSCompression_Title"), decompressResult, InfoBarSeverity.Success, 8000);
                }
                else
                {
                    App.ShowNotification(ResourceString.GetString("OSCompression_Title"), ResourceString.GetString("OSCompression_StatusCompressing"), InfoBarSeverity.Informational, 5000);
                    var compressResult = await CommandExecutor.StartTask("compact.exe /compactos:always");
                    App.ShowNotification(ResourceString.GetString("OSCompression_Title"), compressResult, InfoBarSeverity.Success, 8000);
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"[OSCompression] Failed: {ex.Message}");
            App.ShowNotification("Error", "Failed to modify OS Compression state.", InfoBarSeverity.Error, 5000);
        }
    }

    public async Task<string> GetCompressionStatusAsync()
    {
        return await CommandExecutor.StartTask("compact.exe /compactos:query");
    }
}