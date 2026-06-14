// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Data;

namespace EvolveOS_Optimizer.Core.Converters;

public class IntToDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is int i ? (double)i : 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is double d ? (int)d : 0;
    }
}