// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using EvolveOS_Optimizer.Core.Enums;
using Microsoft.UI.Xaml.Data;

namespace EvolveOS_Optimizer.Core.Converters;

public sealed partial class BadgeKindToStyleConverter : IValueConverter
{
    public static string? GetResourceKey(SettingBadgeKind state) => state switch
    {
        SettingBadgeKind.Recommended => "BadgeRecommendedStyle",
        SettingBadgeKind.Default => "BadgeDefaultStyle",
        SettingBadgeKind.Custom => "BadgeCustomStyle",
        SettingBadgeKind.Preference => "BadgePreferenceStyle",
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

        return Application.Current.Resources.TryGetValue(key, out var style) ? style as Style : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
