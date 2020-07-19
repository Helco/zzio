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

        public static IEnumerable<(T Value, int Index)> Indexed<T>(this IEnumerable<T> set) =>
            set.Select((Value, Index) => (Value, Index));

        public static bool Any<T>(this IEnumerable<T> set, Func<T, int, bool> predicate) =>
            set.Indexed().Any(p => predicate(p.Value, p.Index));
    }
}
