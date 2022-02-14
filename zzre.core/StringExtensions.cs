using System;
using System.Collections.Generic;
using System.Linq;

namespace zzre
{
    public static class StringExtensions
    {
        public static int IndexOfAnyNot(this string thiz, char[] characters) =>
            IndexOfAnyNot(thiz, characters, 0, thiz.Length);

        public static int IndexOfAnyNot(this string thiz, char[] characters, int startIndex) =>
            IndexOfAnyNot(thiz, characters, startIndex, thiz.Length);

        public static int IndexOfAnyNot(this string thiz, char[] characters, int startIndex, int count)
        {
            if (startIndex < 0 || startIndex >= thiz.Length || count <= 0)
                return -1;
            count = Math.Min(count, thiz.Length - startIndex);

            for (int i = startIndex; i < count; i++)
            {
                if (Array.IndexOf(characters, thiz[i]) < 0)
                    return i;
            }
            return -1;
        }
    }
}
