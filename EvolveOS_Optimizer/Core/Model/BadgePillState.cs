// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Core.Model;
public sealed record BadgePillState(
    SettingBadgeKind Kind,
    bool IsHighlighted,
    string Label,
    string Tooltip,
    SettingBadgeMode Mode = SettingBadgeMode.None);
