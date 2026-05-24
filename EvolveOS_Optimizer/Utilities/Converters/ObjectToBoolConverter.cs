// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Data;

namespace EvolveOS_Optimizer.Utilities.Converters
{
    public partial class ObjectToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter is string param && string.Compare(param, "invert", true) == 0)
            {
                var result = Convert(value, targetType, null!, language);

                if (result is bool r)
                    return !r;

                if (result is Visibility v)
                    return v == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }

            object trueValue;
            object falseValue;

            if (targetType == typeof(Visibility))
            {
                trueValue = Visibility.Visible;
                falseValue = Visibility.Collapsed;
            }
            else
            {
                trueValue = true;
                falseValue = false;
            }

            if (value is null)
                return falseValue;

            if (value is string s)
                return string.IsNullOrWhiteSpace(s) || string.IsNullOrEmpty(s) ? falseValue : trueValue;

            var type = value.GetType();
            if (type.IsValueType)
            {
                var defaultValue = Activator.CreateInstance(type);
                return value.Equals(defaultValue) ? falseValue : trueValue;
            }

            return value == default ? falseValue : trueValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
