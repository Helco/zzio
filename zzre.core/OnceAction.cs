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
    public Action? MoveOut()
    {
        var result = Next;
        Next = null;
        return result;
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
    public Action<T1>? MoveOut()
    {
        var result = Next;
        Next = null;
        return result;
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
    public Action<T1, T2>? MoveOut()
    {
        var result = Next;
        Next = null;
        return result;
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
    public Action<T1, T2, T3>? MoveOut()
    {
        var result = Next;
        Next = null;
        return result;
    }
}
