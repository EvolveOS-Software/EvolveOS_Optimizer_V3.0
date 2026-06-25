// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;
using Microsoft.UI.Xaml.Data;

namespace EvolveOS_Optimizer.Core.Converters;

public class GbTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d) return $"{d:F0} GB";
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}