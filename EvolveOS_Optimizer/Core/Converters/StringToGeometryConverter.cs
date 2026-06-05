// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;

namespace EvolveOS_Optimizer.Core.Converters;

public sealed partial class StringToGeometryConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string pathData && !string.IsNullOrEmpty(pathData))
        {
            return XamlBindingHelper.ConvertValue(typeof(Geometry), pathData) as Geometry;
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
