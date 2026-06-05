// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.Win32;

namespace EvolveOS_Optimizer.Core.Model;

public sealed record RegistrySetting
{
    #region Core Identification
    public required string KeyPath { get; init; }
    public string? ValueName { get; init; }
    public required RegistryValueKind ValueType { get; init; }
    #endregion

    #region State Values
    public required object? RecommendedValue { get; init; }
    public required object? DefaultValue { get; init; }
    public object?[]? EnabledValue { get; init; }
    public object?[]? DisabledValue { get; init; }
    #endregion

    #region Scope & Security
    public bool IsPrimary { get; init; } = false;
    public bool IsGroupPolicy { get; init; } = false;
    public bool ApplyPerNetworkInterface { get; init; } = false;
    public bool ApplyPerMonitor { get; init; } = false;
    public bool LockKeyAccess { get; init; } = false;
    #endregion

    #region Advanced Manipulation
    public int? BinaryByteIndex { get; init; }
    public bool ModifyByteOnly { get; init; } = false;
    public byte? BitMask { get; init; }
    public string? CompositeStringKey { get; init; }
    #endregion
}