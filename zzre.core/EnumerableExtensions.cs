using System;
using System.Collections.Generic;
using System.Linq;

namespace zzre
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Except<T>(this IEnumerable<T> set, params T[] values) => set.Except(values);

        public static IEnumerable<T> Generate<T>(int count, Func<int, T> generator) =>
            Enumerable.Range(0, count).Select(generator);
    }
}
