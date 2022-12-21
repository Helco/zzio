using System;

namespace zzre;

public class OnceAction
{
    public event Action? Next = null;
    public void Invoke()
    {
        Next?.Invoke();
        Next = null;
    }
}

public class OnceAction<T1>
{
    public event Action<T1>? Next = null;
    public void Invoke(T1 a)
    {
        Next?.Invoke(a);
        Next = null;
    }
}

public class OnceAction<T1, T2>
{
    public event Action<T1, T2>? Next = null;
    public void Invoke(T1 a, T2 b)
    {
        Next?.Invoke(a, b);
        Next = null;
    }
}

public class OnceAction<T1, T2, T3>
{
    public event Action<T1, T2, T3>? Next = null;
    public void Invoke(T1 a, T2 b, T3 c)
    {
        Next?.Invoke(a, b, c);
        Next = null;
    }
}
