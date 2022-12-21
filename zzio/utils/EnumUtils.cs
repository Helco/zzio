using System;
using System.Text;

namespace zzio;

public static class EnumUtils
{
    public static T intToEnum<T>(int i) where T : struct, IConvertible => Enum.IsDefined(typeof(T), i)
        ? (T)Enum.Parse(typeof(T), i.ToString())
        : (T)Enum.Parse(typeof(T), "Unknown");

    public static T intToFlags<T>(uint value) where T : struct, IConvertible
    {
        var flagString = new StringBuilder();
        for (int bit = 0; bit < 32; bit++)
        {
            int intFlag = 1 << bit;
            if ((value & intFlag) == 0 || !Enum.IsDefined(typeof(T), intFlag))
                continue;
            if (flagString.Length > 0)
                flagString.Append(',');
            flagString.Append(Enum.Parse(typeof(T), intFlag.ToString()));
        }
        return flagString.Length == 0
            ? default
            : (T)Enum.Parse(typeof(T), flagString.ToString());
    }
}
