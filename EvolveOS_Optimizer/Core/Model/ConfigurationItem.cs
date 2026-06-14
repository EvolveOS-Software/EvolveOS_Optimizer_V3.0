// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Converters;

namespace EvolveOS_Optimizer.Core.Model;

public class ConfigurationItem
{
    #region Core Identification
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public InputType InputType { get; set; } = InputType.Toggle;
    #endregion

    #region Feature & Package Management
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(StringOrStringArrayConverter))]
    public string[]? AppxPackageName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WinGetPackageId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CapabilityName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OptionalFeatureName { get; set; }
    #endregion

    #region State & Settings
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsSelected { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SelectedIndex { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? NumericValue { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? CustomStateValues { get; set; }
    #endregion

    #region Power Management
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? PowerSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PowerPlanGuid { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PowerPlanName { get; set; }
    #endregion

    #region Compatibility & Migration
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Obsolete("SelectedValue is only used for backward compatibility during migration. Use SelectedIndex instead.")]
    public string? SelectedValue { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Obsolete("CustomProperties is only used for backward compatibility during migration. Use specific properties instead.")]
    public Dictionary<string, object>? CustomProperties { get; set; }
    #endregion
}
