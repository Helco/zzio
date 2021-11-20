using System;
using System.Collections.Generic;
using System.Linq;

namespace zzio
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

        public static int IndexOf<T>(this IEnumerable<T> set, Func<T, bool> predicate) => set
            .Indexed()
            .Where(p => predicate(p.Value))
            .Select(p => p.Index)
            .Append(-1)
            .First();

        public static int IndexOf<T>(this IEnumerable<T> set, T value) => set
            .Indexed()
            .Where(p => value?.Equals(p.Value) ?? false)
            .Select(prop => prop.Index)
            .Append(-1)
            .First();

        public static Index Offset(this Index index, int offset) => index.IsFromEnd
            ? new Index(index.Value - offset, true)
            : new Index(index.Value + offset, false);

        public static int GetOffset(this Range range, int length) => range.GetOffsetAndLength(length).Offset;
        public static int GetLength(this Range range, int length) => range.GetOffsetAndLength(length).Length;

        public static IEnumerable<T> Range<T>(this IReadOnlyList<T> roList, Range range)
        {
            var (offset, length) = range.GetOffsetAndLength(roList.Count);
            return roList.Skip(offset).Take(length);
        }

        public static T? FirstNotNullOrNull<T>(this IEnumerable<T?> set) where T : class =>
            set.FirstOrDefault(e => e != null);

        public static T FirstNotNull<T>(this IEnumerable<T?> set) where T : class =>
            set.First(e => e != null)!;

        public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> set) where T : struct
        {
            foreach (var element in set)
            {
                if (element.HasValue)
                    yield return element.Value;
            }
        }

        public static IEnumerable<TElement> SelectMany<TElement>(this IEnumerable<IEnumerable<TElement>> set)
            => set.SelectMany(s => s);
    }
}
