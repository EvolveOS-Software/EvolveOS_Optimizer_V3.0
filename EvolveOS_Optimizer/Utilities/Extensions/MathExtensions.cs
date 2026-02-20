using EvolveOS_Optimizer.Core;

namespace EvolveOS_Optimizer.Utilities.Extensions
{
    public static class MathExtensions
    {
        public static bool IsNumber(this object obj)
        {
            if (obj == null) return false;

            switch (Type.GetTypeCode(obj.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;

                default:
                    return false;
            }
        }

        public static KeyValuePair<double, Enums.Memory.Unit> ToMemoryUnit(this long obj)
        {
            if (obj < 1024)
            {
                return new KeyValuePair<double, Enums.Memory.Unit>(obj, Enums.Memory.Unit.B);
            }

            var mag = (int)Math.Log(obj, 1024);

            return new KeyValuePair<double, Enums.Memory.Unit>(obj / Math.Pow(1024, mag), (Enums.Memory.Unit)mag);
        }
    }
}