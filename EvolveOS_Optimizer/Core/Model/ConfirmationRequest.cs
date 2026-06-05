// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record ConfirmationRequest
{
    public string Message { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? CheckboxText { get; init; }
}
