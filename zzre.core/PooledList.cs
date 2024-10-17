using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static zzre.MathEx;

namespace zzre;

public struct PooledList<T> : IDisposable, IEnumerable<T> where T : struct
{
    private readonly T[] array;
    private ArrayPool<T>? pool;
    private int count;

    public readonly int Capacity => array.Length;
    public readonly bool IsFull => count >= array.Length;

    public int Count
    {
        readonly get => count;
        [MethodImpl(MIOptions)] set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, array.Length);
            count = value;
        }
    }

    public readonly ref T this[int index]
    {
        [MethodImpl(MIOptions)]
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, count);
            return ref array[index];
        }
    }
    public readonly ArraySegment<T> ArraySegment => new(array, 0, count);
    public readonly Span<T> Span => array.AsSpan(0, count);
    public readonly Span<T> FullSpan => array.AsSpan();

    [MethodImpl(MIOptions)]
    public void Dispose()
    {
        pool?.Return(array);
        pool = null;
    }

    public PooledList(T[] array) => this.array = array;

    public PooledList(int minCapacity, ArrayPool<T>? pool = null)
    {
        this.pool = pool ?? ArrayPool<T>.Shared;
        array = this.pool.Rent(minCapacity);
    }

    [MethodImpl(MIOptions)]
    public void Clear() => count = 0;

    [MethodImpl(MIOptions)]
    public void Add(in T value)
    {
        if (count >= array.Length)
            throw new InvalidOperationException($"PooledList has no more capacity to add new value");
        array[count++] = value;
    }

    [MethodImpl(MIOptions)]
    public ref T Add()
    {
        if (count >= array.Length)
            throw new InvalidOperationException($"PooledList has no more capacity to add new value");
        return ref array[count++];
    }

    [MethodImpl(MIOptions)]
    public readonly ArraySegment<T>.Enumerator GetEnumerator() =>
        new ArraySegment<T>(array, 0, count).GetEnumerator();
    readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
