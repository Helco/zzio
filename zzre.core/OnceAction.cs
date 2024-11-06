using System;

namespace zzre;

public class OnceAction
{
    public event Action? Next;
    public bool IsEmpty => Next is null;
    public void Invoke()
    {
        var next = Next;
        Next = null;
        next?.Invoke();
    }
}

public class OnceAction<T1>
{
    public event Action<T1>? Next;
    public bool IsEmpty => Next is null;
    public void Invoke(T1 a)
    {
        var next = Next;
        Next = null;
        next?.Invoke(a);
    }
}

public class OnceAction<T1, T2>
{
    public event Action<T1, T2>? Next;
    public bool IsEmpty => Next is null;
    public void Invoke(T1 a, T2 b)
    {
        var next = Next;
        Next = null;
        next?.Invoke(a, b);
    }
}

public class OnceAction<T1, T2, T3>
{
    public event Action<T1, T2, T3>? Next;
    public bool IsEmpty => Next is null;
    public void Invoke(T1 a, T2 b, T3 c)
    {
        var next = Next;
        Next = null;
        next?.Invoke(a, b, c);
    }
}
