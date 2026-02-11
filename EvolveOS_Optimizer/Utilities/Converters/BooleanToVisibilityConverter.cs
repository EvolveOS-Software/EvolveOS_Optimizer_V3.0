using Microsoft.UI.Xaml.Data;

namespace EvolveOS_Optimizer.Utilities.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isVisible = false;
            bool invert = parameter?.ToString()?.Equals("invert", StringComparison.OrdinalIgnoreCase) == true;

            if (value is bool b)
            {
                isVisible = b;
            }
            else
            {
                isVisible = value != null;

                if (value is string s && string.IsNullOrWhiteSpace(s))
                {
                    isVisible = false;
                }

                if (value is System.Collections.IEnumerable enumerable)
                {
                    var enumerator = enumerable.GetEnumerator();
                    isVisible = enumerator.MoveNext();
                }
            }

            if (invert)
            {
                isVisible = !isVisible;
            }

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            bool isVisible = value is Visibility v && v == Visibility.Visible;
            bool invert = parameter?.ToString()?.Equals("invert", StringComparison.OrdinalIgnoreCase) == true;

            return invert ? !isVisible : isVisible;
        }
    }
}