// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

namespace EvolveOS_Optimizer.Utilities.Helpers;

internal static class UnitConversionHelper
{
    public static int ConvertFromSystemUnits(int systemValue, string? displayUnits)
    {
        return displayUnits?.ToLowerInvariant() switch
        {
            "minutes" => systemValue / 60,
            "hours" => systemValue / 3600,
            "milliseconds" => systemValue,
            _ => systemValue
        };
    }
}
