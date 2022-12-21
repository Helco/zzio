using System;

namespace zzio;

public class ResettableLazy<T> : BaseDisposable where T : class
{
    private readonly object? locker;
    private readonly Func<T> creator;
    private T? value;

    public T Value
    {
        get
        {
            if (locker != null)
            {
                lock (locker)
                {
                    if (value == null)
                        value = creator();
                    return value;
                }
            }
            else if (value == null)
                value = creator();
            return value;
        }
    }
    public bool HasValue => value != null;

    public ResettableLazy(Func<T> creator, T? initialValue = null, bool isThreadSafe = false)
    {
        this.creator = creator;
        locker = isThreadSafe ? new object() : null;
        value = initialValue;
    }

    public void Reset()
    {
        if (locker != null)
        {
            lock (locker)
            {
                value = null;
            }
        }
        else
            value = null;
    }

    public static implicit operator T(ResettableLazy<T> lazy) => lazy.Value;
}

// yes this as much code duplication as it gets, what a shame

public class ResettableLazyValue<T> : BaseDisposable where T : struct
{
    private readonly object? locker;
    private readonly Func<T> creator;
    private T? value;

    public T Value
    {
        get
        {
            if (locker != null)
            {
                lock (locker)
                {
                    if (!value.HasValue)
                        value = creator();
                    return value.Value;
                }
            }
            else if (!value.HasValue)
                value = creator();
            return value.Value;
        }
    }
    public bool HasValue => value.HasValue;

    public ResettableLazyValue(Func<T> creator, T? initialValue = null, bool isThreadSafe = false)
    {
        this.creator = creator;
        locker = isThreadSafe ? new object() : null;
        value = initialValue;
    }

    public void Reset()
    {
        if (locker != null)
        {
            lock (locker)
            {
                value = null;
            }
        }
        else
            value = null;
    }

    public static implicit operator T(ResettableLazyValue<T> lazy) => lazy.Value;
}
