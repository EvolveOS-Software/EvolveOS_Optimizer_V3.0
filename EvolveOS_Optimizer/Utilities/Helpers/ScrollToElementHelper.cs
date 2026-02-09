using Microsoft.UI.Xaml.Input;

namespace EvolveOS_Optimizer.Utilities.Helpers;

public static class ScrollToElementHelper
{
    private static FrameworkElement? _highlightedElement;
    private static Brush? _originalBackground;
    private static Brush? _originalBorderBrush;
    private static Thickness _originalBorderThickness;
    private static Page? _currentPage;

    public static async Task ScrollToElementAsync(DependencyObject root, string? optionTag)
    {
        if (string.IsNullOrEmpty(optionTag))
        {
            return;
        }

        ClearHighlight();

        await Task.Delay(100);

        var scrollViewer = FindChild<ScrollViewer>(root);
        if (scrollViewer == null)
        {
            return;
        }

        var targetElement = FindElementByTag(root, optionTag);
        if (targetElement == null)
        {
            return;
        }

        var settingsCard = FindParentSettingsCard(targetElement);
        var elementToScrollTo = settingsCard ?? targetElement;

        var transform = elementToScrollTo.TransformToVisual(scrollViewer);
        var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

        var scrollOffset = Math.Max(0, position.Y + scrollViewer.VerticalOffset - 100);
        scrollViewer.ChangeView(null, scrollOffset, null, false);

        HighlightElementUntilClick(elementToScrollTo, root);
    }

    public static void ClearHighlight()
    {
        if (_highlightedElement != null)
        {
            try
            {
                if (_highlightedElement is Control control)
                {
                    control.Background = _originalBackground;
                    control.BorderBrush = _originalBorderBrush;
                    control.BorderThickness = _originalBorderThickness;
                }
                else if (_highlightedElement is Panel panel)
                {
                    panel.Background = _originalBackground;
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }

            _highlightedElement = null;
            _originalBackground = null;
            _originalBorderBrush = null;
        }

        if (_currentPage != null)
        {
            _currentPage.PointerPressed -= Page_PointerPressed;
            _currentPage = null;
        }
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null)
        {
            return null;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild)
            {
                return typedChild;
            }

            var result = FindChild<T>(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static FrameworkElement? FindElementByTag(DependencyObject parent, string tag)
    {
        if (parent == null)
        {
            return null;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is FrameworkElement element && element.Tag is string elementTag &&
                elementTag.Equals(tag, StringComparison.OrdinalIgnoreCase))
            {
                return element;
            }

            if (child is FrameworkElement namedElement &&
                namedElement.Name.Equals(tag, StringComparison.OrdinalIgnoreCase))
            {
                return namedElement;
            }

            var result = FindElementByTag(child, tag);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static FrameworkElement? FindParentSettingsCard(DependencyObject element)
    {
        var parent = VisualTreeHelper.GetParent(element);

        while (parent != null)
        {
            if (parent.GetType().Name == "SettingsCard" && parent is FrameworkElement frameworkElement)
            {
                return frameworkElement;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    private static void HighlightElementUntilClick(FrameworkElement element, DependencyObject root)
    {
        try
        {
            if (element is Control control)
            {
                _originalBackground = control.Background;
                _originalBorderBrush = control.BorderBrush;
                _originalBorderThickness = control.BorderThickness;

                var highlightBrush = Application.Current.Resources["SystemAccentColor"] is Windows.UI.Color accentColor
                    ? new SolidColorBrush(accentColor) { Opacity = 0.2 }
                    : new SolidColorBrush(Microsoft.UI.Colors.Yellow) { Opacity = 0.2 };

                var borderBrush = Application.Current.Resources["AccentAAFillColorDefaultBrush"] as SolidColorBrush
                    ?? new SolidColorBrush(Microsoft.UI.Colors.Orange);

                control.Background = highlightBrush;
                control.BorderBrush = borderBrush;
                control.BorderThickness = new Thickness(2);

                _highlightedElement = element;
            }
            else if (element is Panel panel)
            {
                _originalBackground = panel.Background;

                var highlightBrush = new SolidColorBrush(Microsoft.UI.Colors.Yellow) { Opacity = 0.2 };
                panel.Background = highlightBrush;

                _highlightedElement = element;
            }

            var page = FindParent<Page>(element);
            if (page != null)
            {
                _currentPage = page;
                page.PointerPressed += Page_PointerPressed;
            }
        }
        catch
        {
            // Ignore any errors during highlighting
        }
    }

    private static void Page_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ClearHighlight();
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);

        while (parent != null)
        {
            if (parent is T typedParent)
            {
                return typedParent;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }
}
