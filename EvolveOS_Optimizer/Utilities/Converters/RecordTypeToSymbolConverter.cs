// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Data;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Converters
{
    public class RecordTypeToSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is RecordType recordType)
            {
                return recordType switch
                {
                    RecordType.Website => "Globe24",
                    RecordType.Email => "Mail24",
                    RecordType.Mobile => "Phone24",
                    RecordType.Official => "Document24",
                    RecordType.Bank => "Wallet24",
                    RecordType.Other => "Note24",
                    RecordType.All => "DataArea24",
                    _ => "DataArea24",
                };
            }
            return "DataArea24";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}