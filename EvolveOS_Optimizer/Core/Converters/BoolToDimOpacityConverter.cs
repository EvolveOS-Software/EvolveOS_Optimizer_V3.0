// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System;
using Microsoft.UI.Xaml.Data;

namespace EvolveOS_Optimizer.Core.Converters;

public sealed partial class BoolToDimOpacityConverter : IValueConverter
{
    private const double Highlighted = 1.0;
    private const double Dim = 0.35;

    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool isHighlighted ? (isHighlighted ? Highlighted : Dim) : Highlighted;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
