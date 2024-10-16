using System;
using System.Runtime.CompilerServices;
using static zzre.MathEx;

namespace zzre;

public ref struct ListOverSpan<T>(Span<T> span)
{
    private readonly Span<T> span = span;
    private int count;

    public ListOverSpan(Span<T> span, int initialCount) : this(span)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(initialCount, span.Length);
        count = initialCount;
    }

    public readonly int Count => count;
    public readonly int Capacity => span.Length;
    public readonly bool IsFull => count >= span.Length;

    public readonly ref T this[int index]
    {
        [MethodImpl(MIOptions)]
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, count);
            return ref span[index];
        }
    }
    public readonly Span<T> Span => span;

    [MethodImpl(MIOptions)]
    public void Clear() => count = 0;

    [MethodImpl(MIOptions)]
    public void Add(in T value)
    {
        if (count >= span.Length)
            throw new InvalidOperationException($"ListOverSpan has no more capacity to add new value");
        span[count++] = value;
    }

    [MethodImpl(MIOptions)]
    public ref T Add()
    {
        if (count >= span.Length)
            throw new InvalidOperationException($"ListOverSpan has no more capacity to add new value");
        return ref span[count++];
    }

    [MethodImpl(MIOptions)]
    public readonly Span<T>.Enumerator GetEnumerator() => span[0..count].GetEnumerator();
}
