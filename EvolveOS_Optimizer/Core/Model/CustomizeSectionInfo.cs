// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Core.Model;

public class CustomizeSectionInfo : ISectionInfo
{
    public string Key { get; }
    public string IconGlyphKey { get; }
    public string DisplayName { get; }
    public string ModuleId { get; }

    public CustomizeSectionInfo(string key, string iconGlyphKey, string displayName, string moduleId)
    {
        Key = key;
        IconGlyphKey = iconGlyphKey;
        DisplayName = displayName;
        ModuleId = moduleId;
    }
}
