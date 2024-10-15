using System;
using System.Runtime.CompilerServices;
using static zzre.MathEx;

namespace zzre;

[method: MethodImpl(MIOptions)]
public ref struct StackOverSpan<T>(Span<T> span)
{
    private readonly Span<T> span = span;
    private int count;

    public readonly int Count => count;
    public readonly int Capacity => span.Length;

    [MethodImpl(MIOptions)]
    public void Clear() => count = 0;

    [MethodImpl(MIOptions)]
    public readonly ref T Peek()
    {
        if (count == 0)
            throw new InvalidOperationException("Cannot peek into stack, stack is empty");
        return ref span[count - 1];
    }

    [MethodImpl(MIOptions)]
    public void Push(in T value)
    {
        if (count >= span.Length)
            throw new InvalidOperationException("Cannot push on stack, stack is full");
        span[count++] = value;
    }

    [MethodImpl(MIOptions)]
    public ref T Push()
    {
        if (count >= span.Length)
            throw new InvalidOperationException("Cannot push on stack, stack is full");
        return ref span[count++];
    }

    [MethodImpl(MIOptions)]
    public ref readonly T Pop()
    {
        if (count == 0)
            throw new InvalidOperationException("Cannot pop from stack, stack is empty");
        return ref span[--count];
    }

    [MethodImpl(MIOptions)]
    public bool TryPop(out T value)
    {
        if (count == 0)
        {
            value = default!;
            return false;
        }
        else
        {
            value = span[--count];
            return true;
        }
    }
}
