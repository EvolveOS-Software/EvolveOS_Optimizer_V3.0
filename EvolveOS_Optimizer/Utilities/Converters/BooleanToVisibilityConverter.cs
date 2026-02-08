using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace EvolveOS_Optimizer.Utilities.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b)
            {
                bool invert = parameter?.ToString()?.Equals("invert", StringComparison.OrdinalIgnoreCase) == true;

                if (invert)
                {
                    b = !b;
                }

                return b ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }
}