// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Input;

namespace EvolveOS_Optimizer.Utilities.Helpers;

internal static class PageScrollHelper
{
    #region Constants

    private const double PageStepFraction = 0.15;

    #endregion

    #region Public Methods
    public static void Attach(UIElement keyEventSource, ScrollView scrollView)
    {
        if (keyEventSource == null || scrollView == null) return;

        keyEventSource.AddHandler(
            UIElement.PreviewKeyDownEvent,
            new KeyEventHandler((s, e) => HandleKey(scrollView, e)),
            handledEventsToo: true);

        keyEventSource.AddHandler(
            UIElement.KeyDownEvent,
            new KeyEventHandler((s, e) => HandleKey(scrollView, e)),
            handledEventsToo: true);
    }

    public static void HandleKey(ScrollView scrollView, KeyRoutedEventArgs e)
    {
        if (scrollView == null || e == null) return;
        if (!IsPagingKey(e.Key)) return;

        if (ShouldSkipForFocusedElement(e.OriginalSource as DependencyObject, scrollView))
            return;

        if (scrollView.ScrollableHeight <= 0) return;

        var options = new ScrollingScrollOptions(ScrollingAnimationMode.Disabled);

        switch (e.Key)
        {
            case VirtualKey.PageUp:
                scrollView.ScrollBy(0, -scrollView.ViewportHeight * PageStepFraction, options);
                e.Handled = true;
                break;

            case VirtualKey.PageDown:
                scrollView.ScrollBy(0, scrollView.ViewportHeight * PageStepFraction, options);
                e.Handled = true;
                break;

            case VirtualKey.Home:
                scrollView.ScrollTo(scrollView.HorizontalOffset, 0, options);
                e.Handled = true;
                break;

            case VirtualKey.End:
                scrollView.ScrollTo(scrollView.HorizontalOffset, scrollView.ScrollableHeight, options);
                e.Handled = true;
                break;
        }
    }
    #endregion

    #region Internal Helpers
    internal static bool IsPagingKey(VirtualKey key) =>
        key == VirtualKey.PageUp ||
        key == VirtualKey.PageDown ||
        key == VirtualKey.Home ||
        key == VirtualKey.End;

    internal static bool ShouldSkipForFocusedElement(DependencyObject? focused, ScrollView scrollViewHost)
    {
        for (var current = focused; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ScrollViewer svr && svr.VerticalScrollMode != ScrollMode.Disabled)
                return true;

            if (current is ScrollView sv
                && !ReferenceEquals(sv, scrollViewHost)
                && sv.VerticalScrollMode != ScrollingScrollMode.Disabled)
                return true;

            if (current is ComboBox combo && combo.IsDropDownOpen) return true;

            if (current is AutoSuggestBox asb && asb.IsSuggestionListOpen) return true;

            if (current is TextBox tb && (tb.AcceptsReturn || tb.TextWrapping != TextWrapping.NoWrap))
                return true;
        }

        return false;
    }
    #endregion
}
