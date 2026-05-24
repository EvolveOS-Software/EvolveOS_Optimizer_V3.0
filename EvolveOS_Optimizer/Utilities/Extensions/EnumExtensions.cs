// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.Reflection;
using EvolveOS_Optimizer.Core;
using EvolveOS_Optimizer.Core.Model;

namespace EvolveOS_Optimizer.Utilities.Extensions
{
    public static class EnumExtensions
    {
        public static object? DefaultValue(this Type obj)
        {
            return obj.IsValueType && Nullable.GetUnderlyingType(obj) == null ? Activator.CreateInstance(obj) : null;
        }

        public static KeyValuePair<string?, bool?> GetKeyValue(this Enums.Dialog.Button obj)
        {
            switch (obj)
            {
                case Enums.Dialog.Button.None:
                    return new KeyValuePair<string?, bool?>(null, null);

                case Enums.Dialog.Button.Yes:
                    return new KeyValuePair<string?, bool?>("Yes", true);

                case Enums.Dialog.Button.No:
                    return new KeyValuePair<string?, bool?>("No", false);

                default:
                    throw new NotImplementedException();
            }
        }

        public static string GetString(this Enums.Memory.Optimization.Reason obj)
        {
            switch (obj)
            {
                case Enums.Memory.Optimization.Reason.LowMemory:
                    return "Low memory";

                case Enums.Memory.Optimization.Reason.Manual:
                    return "Manual";

                case Enums.Memory.Optimization.Reason.Schedule:
                    return "Schedule";

                default:
                    throw new NotImplementedException();
            }
        }

        public static bool IsValid(this Enum obj)
        {
            if (obj == null) return false;

            var firstDigit = obj.ToString()[0];
            return !char.IsDigit(firstDigit) && firstDigit != '-';
        }

        public static int IndexOf<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            var index = 0;

            foreach (var item in source)
            {
                if (predicate(item)) return index;

                index++;
            }

            return -1;
        }

        public static TEnum GetEnum<TEnum>(string text) where TEnum : struct
        {
            if (!typeof(TEnum).GetTypeInfo().IsEnum)
            {
                throw new InvalidOperationException("Generic parameter 'TEnum' must be an enum.");
            }
            return Enum.Parse<TEnum>(text);
        }

        public static IEnumerable<KeyItem> GetFlattenNodes(this IEnumerable<KeyItem> masterList)
        {
            foreach (var node in masterList)
            {
                yield return node;

                foreach (var children in node.Children.GetFlattenNodes())
                {
                    yield return children;
                }
            }
        }
    }
}