// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Core.Model;

public class SearchSuggestionItem
{
    #region Properties
    public string SettingName { get; set; } = string.Empty;
    public string SectionKey { get; set; } = string.Empty;
    public string SectionDisplayName { get; set; } = string.Empty;
    public string SectionIconGlyph { get; set; } = string.Empty;
    public string DisplayText => $"{SettingName} ({SectionDisplayName})";
    #endregion

    #region Constructors
    public SearchSuggestionItem()
    {
    }

    public SearchSuggestionItem(string settingName, string sectionKey, string sectionDisplayName, string sectionIconGlyph)
    {
        SettingName = settingName;
        SectionKey = sectionKey;
        SectionDisplayName = sectionDisplayName;
        SectionIconGlyph = sectionIconGlyph;
    }
    #endregion
}