// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EvolveOS_Optimizer.Core.ViewModel;

public partial class BuilderFeatureCategory : ObservableObject
{
    public string FeatureId { get; }
    public string DisplayName { get; }
    public string IconGlyph { get; }

    public ObservableCollection<SettingItemViewModel> Settings { get; } = new();

    public BuilderFeatureCategory(string featureId, string displayName, string iconGlyph)
    {
        FeatureId = featureId;
        DisplayName = displayName;
        IconGlyph = iconGlyph;
    }
}