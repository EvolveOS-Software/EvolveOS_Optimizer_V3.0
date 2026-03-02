using Microsoft.UI.Xaml.Data;

namespace EvolveOS_Optimizer.Utilities.Converters
{
    public class UserTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string userType = value as string ?? "Guest";

            if (userType.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(Colors.Red);
            }

            return new SolidColorBrush(ColorHelper.FromArgb(255, 120, 120, 120));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
