using System;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using static zzre.MathEx;

namespace zzre;

public ref struct StackOverSpan<T>
{
    private readonly Span<T> span;
    private int count;

    [MethodImpl(MIOptions)]
    public StackOverSpan(Span<T> span) => this.span = span;

    public readonly int Count => count;
    public readonly int Capacity => span.Length;

    [MethodImpl(MIOptions)]
    public void Clear() => count = 0;

    [MethodImpl(MIOptions)]
    public readonly ref T Peek()
    {
        Debug.Assert(span != null);
        if (count == 0)
            throw new InvalidOperationException("Cannot peek into stack, stack is empty");
        return ref span[count];
    }

    [MethodImpl(MIOptions)]
    public void Push(in T value)
    {
        Debug.Assert(span != null);
        if (count >= span.Length)
            throw new InvalidOperationException("Cannot push on stack, stack is full");
        span[count++] = value;
    }

    [MethodImpl(MIOptions)]
    public ref T Push()
    {
        Debug.Assert(span != null);
        if (count >= span.Length)
            throw new InvalidOperationException("Cannot push on stack, stack is full");
        return ref span[count++];
    }

    [MethodImpl(MIOptions)]
    public ref readonly T Pop()
    {
        Debug.Assert(span != null);
        if (count == 0)
            throw new InvalidOperationException("Cannot pop from stack, stack is empty");
        return ref span[--count];
    }

    [MethodImpl(MIOptions)]
    public bool TryPop(out T value)
    {
        Debug.Assert(span != null);
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
