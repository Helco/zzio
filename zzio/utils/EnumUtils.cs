using System;

namespace zzio.utils
{
    public static class EnumUtils
    {
        public static T intToEnum<T>(int i) where T : struct, IConvertible
        {
            if (Enum.IsDefined(typeof(T), i))
                return (T)Enum.Parse(typeof(T), i.ToString());
            else
                return (T)Enum.Parse(typeof(T), "Unknown");
        }

        public static T intToFlags<T>(uint value) where T : struct, IConvertible
        {
            string flagString = "";
            for (int bit = 0; bit < 32; bit++)
            {
                int intFlag = 1 << bit;
                if ((value & intFlag) > 0 && Enum.IsDefined(typeof(T), intFlag))
                    flagString += "," + Enum.Parse(typeof(T), intFlag.ToString()).ToString();
            }
            if (flagString.Length == 0)
                return default(T);
            else
                return (T)Enum.Parse(typeof(T), flagString.Substring(1));
        }
    }
}
