// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;

namespace EvolveOS_Optimizer.Core.Converters;

internal static class NumericConversionHelper
{
    public static int ConvertNumericValue(object value)
    {
        return value switch
        {
            int intVal => intVal,
            long longVal => (int)longVal,
            double doubleVal => (int)doubleVal,
            float floatVal => (int)floatVal,
            string stringVal when int.TryParse(stringVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
            JsonElement je when je.TryGetInt32(out int jsonInt) => jsonInt,
            _ => throw new ArgumentException($"Cannot convert '{value}' (type: {value?.GetType().Name ?? "null"}) to numeric value")
        };
    }
}
