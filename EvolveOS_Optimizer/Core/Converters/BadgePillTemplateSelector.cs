// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Core.Converters;

public sealed partial class BadgePillTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RecommendedTemplate { get; set; }
    public DataTemplate? DefaultTemplate { get; set; }
    public DataTemplate? CustomTemplate { get; set; }
    public DataTemplate? PreferenceTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is not BadgePillState pill) return null;
        return PickByKind(pill.Kind, RecommendedTemplate, DefaultTemplate, CustomTemplate, PreferenceTemplate);
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);

    public static T? PickByKind<T>(SettingBadgeKind kind, T? recommended, T? @default, T? custom, T? preference)
        where T : class
        => kind switch
        {
            SettingBadgeKind.Recommended => recommended,
            SettingBadgeKind.Default     => @default,
            SettingBadgeKind.Custom      => custom,
            SettingBadgeKind.Preference  => preference,
            _                            => null,
        };
}
