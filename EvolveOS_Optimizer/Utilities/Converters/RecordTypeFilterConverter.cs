// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Reflection;
using Microsoft.UI.Xaml.Data;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Converters
{
    public class RecordTypeFilterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is IEnumerable items)
            {
                return items.Cast<object>().Where(ExcludeAllFilter).ToList();
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }

        private bool ExcludeAllFilter(object? item)
        {
            if (item == null) return true;

            if (item is RecordType type)
            {
                return type != RecordType.All;
            }

            var itemType = item.GetType();
            var valueProperty = itemType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);

            if (valueProperty != null && valueProperty.PropertyType == typeof(RecordType))
            {
                object? propValue = valueProperty.GetValue(item);
                if (propValue is RecordType recordValue)
                {
                    return recordValue != RecordType.All;
                }
            }

            return true;
        }
    }
}