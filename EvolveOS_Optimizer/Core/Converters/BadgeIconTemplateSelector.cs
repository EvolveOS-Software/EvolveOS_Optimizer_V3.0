// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Core.Converters;

public sealed partial class BadgeIconTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RecommendedTemplate { get; set; }
    public DataTemplate? DefaultTemplate { get; set; }
    public DataTemplate? CustomTemplate { get; set; }
    public DataTemplate? PreferenceTemplate { get; set; }

    public static T? PickByState<T>(
        SettingBadgeKind state,
        T? recommended,
        T? @default,
        T? custom,
        T? preference)
        where T : class
        => state switch
        {
            SettingBadgeKind.Recommended => recommended,
            SettingBadgeKind.Default => @default,
            SettingBadgeKind.Custom => custom,
            SettingBadgeKind.Preference => preference,
            _ => null,
        };

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is not SettingBadgeKind state)
        {
            return null;
        }

        return PickByState(state, RecommendedTemplate, DefaultTemplate, CustomTemplate, PreferenceTemplate);
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
