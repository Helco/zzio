using System;
using System.Text;

namespace zzio;

public static class EnumUtils
{
    public static T intToEnum<T>(int i) where T : struct, Enum => Enum.IsDefined(typeof(T), i)
        ? Enum.Parse<T>(i.ToString())
        : Enum.Parse<T>("Unknown");

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
            flagString.Append(Enum.Parse<T>(intFlag.ToString()));
        }
        return flagString.Length == 0
            ? default
            : Enum.Parse<T>(flagString.ToString());
    }
}
