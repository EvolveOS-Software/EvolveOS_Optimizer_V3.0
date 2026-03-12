using Microsoft.UI.Xaml.Data;

namespace EvolveOS_Optimizer.Utilities.Converters
{
    public class ObjectEqualityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == DependencyProperty.UnsetValue)
            {
                return false;
            }

            if (value == null && parameter == null)
            {
                return true;
            }

            if (value == null || parameter == null)
            {
                return false;
            }

            return value.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}