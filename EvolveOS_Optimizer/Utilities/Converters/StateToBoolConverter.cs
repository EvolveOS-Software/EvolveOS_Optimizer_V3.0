using Microsoft.UI.Xaml.Data;

namespace EvolveOS_Optimizer.Utilities.Converters
{
    public class StateToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int state) return state == 1;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return (bool)value ? 1 : 0;
    }
}

    public class StateToEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int state) return state != 2;
            return true;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}