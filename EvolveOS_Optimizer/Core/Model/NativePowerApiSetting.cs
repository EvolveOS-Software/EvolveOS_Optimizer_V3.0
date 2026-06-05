// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public sealed record NativePowerApiSetting
{
    public int InformationLevel { get; init; }
    public byte EnabledValue { get; init; }
    public byte DisabledValue { get; init; }
}
