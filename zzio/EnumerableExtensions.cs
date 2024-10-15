using System;
using System.Collections.Generic;
using System.Linq;

namespace zzio;

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
        .Where(p => value?.Equals(p.Value) ?? p.Value is null)
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

    public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> set) where T : class
    {
        foreach (var element in set)
        {
            if (element != null)
                yield return element;
        }
    }

    public static IEnumerable<TElement> SelectMany<TElement>(this IEnumerable<IEnumerable<TElement>> set)
        => set.SelectMany(s => s);

    public static TCompare? MaxOrDefault<TElement, TCompare>(this IEnumerable<TElement> set, Func<TElement, TCompare> selector, TCompare? defaultValue = default) =>
        set.Any() ? set.Max(selector) : defaultValue;

    public static TElement? MinByOrDefault<TElement, TKey>(this IEnumerable<TElement> set, Func<TElement, TKey> compare) =>
        set.Any() ? set.MinBy(compare) : default;

    public delegate bool ReferencePredicate<TElement>(in TElement element);

    public static int Count<TElement>(this ReadOnlySpan<TElement> span, ReferencePredicate<TElement> predicate)
    {
        int count = 0;
        foreach (ref readonly var element in span)
        {
            if (predicate(in element))
                count++;
        }
        return count;
    }

    public static int Count<TElement>(this Span<TElement> span, ReferencePredicate<TElement> predicate)
    {
        int count = 0;
        foreach (ref readonly var element in span)
        {
            if (predicate(in element))
                count++;
        }
        return count;
    }

    public static Range Sub(this Range full, int offset, int count, int maxValue = int.MaxValue) =>
        Sub(full, offset..(offset + count), maxValue);

    public static Range Sub(this Range full, Range sub, int maxValue = int.MaxValue)
    {
        var (fullOffset, fullLength) = full.GetOffsetAndLength(maxValue);
        var (subOffset, subLength) = sub.GetOffsetAndLength(fullLength);
        int newOffset = fullOffset + subOffset;
        return newOffset..(newOffset + subLength);
    }

    public static IEnumerable<TOutput> PrefixSums<TInput, TOutput>(
        this IEnumerable<TInput> set, TOutput first, Func<TOutput, TInput, TOutput> next)
    {
        foreach (var input in set)
        {
            yield return first;
            first = next(first, input);
        }
    }

    public static IEnumerable<TOutput> PrefixSumsInclusive<TInput, TOutput>(
        this IEnumerable<TInput> set, TOutput first, Func<TOutput, TInput, TOutput> next)
    {
        foreach (var input in set)
        {
            yield return first;
            first = next(first, input);
        }
        yield return first;
    }
}
