using System.Text.Json.Serialization;

namespace EvolveOS_Optimizer.Core.Model.Profiles;

/// <summary>
/// Represents a complete, exportable EvolveOS Configuration Profile.
/// </summary>
public class EvolveOSProfile
{
    [JsonPropertyName("profileVersion")]
    public string ProfileVersion { get; set; } = "1.0";

    [JsonPropertyName("exportDateUtc")]
    public DateTime ExportDateUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = "1.1.5.331"; // Or whatever your current version is

    // Keeps Optimize and Customize separated for clear organization
    [JsonPropertyName("optimize")]
    public ProfileCategory Optimize { get; set; } = new();

    [JsonPropertyName("customize")]
    public ProfileCategory Customize { get; set; } = new();
}

public class ProfileCategory
{
    // Dictionary Key = Feature ID (e.g., "Privacy", "Taskbar")
    [JsonPropertyName("features")]
    public Dictionary<string, ProfileFeature> Features { get; set; } = new();
}

public class ProfileFeature
{
    // A list of all setting states active for this specific feature
    [JsonPropertyName("settings")]
    public List<ProfileSettingItem> Settings { get; set; } = new();
}

/// <summary>
/// A raw, lightweight representation of a specific setting's state.
/// </summary>
public class ProfileSettingItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    // Toggles and Checkboxes (True/False)
    [JsonPropertyName("isEnabled")]
    public bool? IsEnabled { get; set; }

    // Dropdowns and Selection inputs
    [JsonPropertyName("selectedIndex")]
    public int? SelectedIndex { get; set; }

    public double? NumericValue { get; set; }

    // Advanced/Custom states (e.g., Power plan GUIDs, numeric ranges)
    [JsonPropertyName("customValue")]
    public object? CustomValue { get; set; }
}