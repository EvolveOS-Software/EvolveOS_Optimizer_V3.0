// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Reflection;

namespace EvolveOS_Optimizer.Core.Model
{
    public enum RecordType
    {
        [Description("All Records")]
        All,
        [Description("Website Login")]
        Website,
        [Description("Email Account")]
        Email,
        [Description("Mobile App/Service")]
        Mobile,
        [Description("Official Document/System")]
        Official,
        [Description("Bank Account / Financial")]
        Bank,
        [Description("Other / General Secure Note")]
        Other
    }

    public static class EnumExtensions
    {
        public static string GetDescription(this System.Enum value)
        {
            FieldInfo? field = value.GetType().GetField(value.ToString());

            if (field == null) return value.ToString();
            DescriptionAttribute? attribute = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;

            return attribute?.Description ?? value.ToString();
        }
    }
}