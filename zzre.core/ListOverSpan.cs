using System;
using System.Runtime.CompilerServices;
using static zzre.MathEx;

namespace zzre;

public ref struct ListOverSpan<T>(Span<T> span)
{
    public ListOverSpan(Span<T> span, int initialCount) : this(span)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(initialCount, span.Length);
        Count = initialCount;
    }

    public int Count { readonly get; private set; }
    public readonly int Capacity => FullSpan.Length;
    public readonly bool IsFull => Count >= FullSpan.Length;
    public readonly Span<T> FullSpan { get; } = span;
    public readonly Span<T> Span => FullSpan[0..Count];

    public readonly ref T this[int index]
    {
        [MethodImpl(MIOptions)]
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
            return ref FullSpan[index];
        }
    }

    [MethodImpl(MIOptions)]
    public void Clear() => Count = 0;

    [MethodImpl(MIOptions)]
    public void Add(in T value)
    {
        if (Count >= FullSpan.Length)
            throw new InvalidOperationException($"ListOverSpan has no more capacity to add new value");
        FullSpan[Count++] = value;
    }

    [MethodImpl(MIOptions)]
    public ref T Add()
    {
        if (Count >= FullSpan.Length)
            throw new InvalidOperationException($"ListOverSpan has no more capacity to add new value");
        return ref FullSpan[Count++];
    }

    [MethodImpl(MIOptions)]
    public readonly Span<T>.Enumerator GetEnumerator() => FullSpan[0..Count].GetEnumerator();
}
