// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Interfaces;

public interface IDialogService
{
    void ShowMessage(string message, string title = "");

    Task<bool> ShowConfirmationAsync(string message, string title = "", string okButtonText = "OK", string cancelButtonText = "Cancel");

    Task ShowInformationAsync(string message, string title = "Information", string buttonText = "OK");

    Task ShowWarningAsync(string message, string title = "Warning", string buttonText = "OK");

    Task ShowErrorAsync(string message, string title = "Error", string buttonText = "OK");

    Task<(ImportOption? Option, ImportOptions Options)> ShowConfigImportOptionsDialogAsync();

    Task<(bool Confirmed, bool CheckboxChecked)> ShowConfirmationWithCheckboxAsync(
        string message,
        string? checkboxText = null,
        string title = "Confirmation",
        string continueButtonText = "Continue",
        string cancelButtonText = "Cancel");

    Task<(bool Confirmed, bool CheckboxChecked)> ShowAppOperationConfirmationAsync(
        string operationType,
        IEnumerable<string> itemNames,
        int count,
        string? checkboxText = null);

    Task<ConfirmationResponse> ShowConfirmationAsync(
        ConfirmationRequest confirmationRequest,
        string continueButtonText = "Continue",
        string cancelButtonText = "Cancel");

    Task ShowTaskOutputDialogAsync(string title, IReadOnlyList<string> logMessages);

    Task ShowCustomContentDialogAsync(string title, object content, string closeButtonText = "Close");
}
