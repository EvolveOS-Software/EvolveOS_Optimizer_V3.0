// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record ConfirmationResponse
{
    public bool Confirmed { get; init; }
    public bool CheckboxChecked { get; init; }
}
