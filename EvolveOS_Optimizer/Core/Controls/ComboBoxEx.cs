// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace EvolveOS_Optimizer.Core.Controls;

public partial class ComboBoxEx : ComboBox
{
    private double _cachedWidth;

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        if (!IsDropDownOpen && (e.Key == VirtualKey.Up || e.Key == VirtualKey.Down))
        {
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnDropDownOpened(object e)
    {
        var widthToApply = _cachedWidth > 0 ? _cachedWidth : ActualWidth;
        if (widthToApply > 0)
            Width = widthToApply;

        base.OnDropDownOpened(e);
    }

    protected override void OnDropDownClosed(object e)
    {
        Width = double.NaN;
        base.OnDropDownClosed(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var baseSize = base.MeasureOverride(availableSize);

        if (baseSize.Width > 64)
            _cachedWidth = baseSize.Width;

        return baseSize;
    }
}
