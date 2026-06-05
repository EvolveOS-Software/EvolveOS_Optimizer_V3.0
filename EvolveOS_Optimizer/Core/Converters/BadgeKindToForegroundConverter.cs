// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Data;
using EvolveOS_Optimizer.Core.Enums;

namespace EvolveOS_Optimizer.Core.Converters;

public sealed partial class BadgeKindToForegroundConverter : IValueConverter
{
    public static string? GetResourceKey(SettingBadgeKind state) => state switch
    {
        SettingBadgeKind.Recommended => "BadgeRecommendedForeground",
        SettingBadgeKind.Default => "BadgeDefaultForeground",
        SettingBadgeKind.Custom => "BadgeCustomForeground",
        SettingBadgeKind.Preference => "BadgePreferenceForeground",
        _ => null,
    };

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not SettingBadgeKind state)
        {
            return null;
        }

        var key = GetResourceKey(state);
        if (key is null)
        {
            return null;
        }

        return Application.Current.Resources.TryGetValue(key, out var brush) ? brush as Brush : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
