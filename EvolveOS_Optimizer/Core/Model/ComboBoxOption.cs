// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Core.Model;

public sealed record ComboBoxOption
{
    #region Basic Metadata
    public required string DisplayName { get; init; }
    public string? Tooltip { get; init; }
    public string? Warning { get; init; }
    public (string Title, string Message)? Confirmation { get; init; }
    #endregion

    #region Value Mapping
    public Dictionary<string, object?>? ValueMappings { get; init; }
    public int? SimpleValue { get; init; }
    public bool? CommandValue { get; init; }
    #endregion

    #region Scripting & Automation
    public ScriptOption? Script { get; init; }
    public Dictionary<string, string>? ScriptVariables { get; init; }
    #endregion

    #region State Configuration
    public bool IsDefault { get; init; }
    public bool IsRecommended { get; init; }
    #endregion
}
