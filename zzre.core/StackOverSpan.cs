using System;
using System.Runtime.CompilerServices;
using static zzre.MathEx;

namespace zzre;

[method: MethodImpl(MIOptions)]
public ref struct StackOverSpan<T>(Span<T> span)
{
    private readonly Span<T> span = span;

    public int Count { readonly get; private set; }
    public readonly int Capacity => span.Length;

    [MethodImpl(MIOptions)]
    public void Clear() => Count = 0;

    [MethodImpl(MIOptions)]
    public readonly ref T Peek()
    {
        if (Count == 0)
            throw new InvalidOperationException("Cannot peek into stack, stack is empty");
        return ref span[Count - 1];
    }

    [MethodImpl(MIOptions)]
    public void Push(in T value)
    {
        if (Count >= span.Length)
            throw new InvalidOperationException("Cannot push on stack, stack is full");
        span[Count++] = value;
    }

    [MethodImpl(MIOptions)]
    public ref T Push()
    {
        if (Count >= span.Length)
            throw new InvalidOperationException("Cannot push on stack, stack is full");
        return ref span[Count++];
    }

    [MethodImpl(MIOptions)]
    public ref readonly T Pop()
    {
        if (Count == 0)
            throw new InvalidOperationException("Cannot pop from stack, stack is empty");
        return ref span[--Count];
    }

    [MethodImpl(MIOptions)]
    public bool TryPop(out T value)
    {
        if (Count == 0)
        {
            value = default!;
            return false;
        }
        else
        {
            value = span[--Count];
            return true;
        }
    }
}
