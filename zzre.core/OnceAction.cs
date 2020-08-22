using System;

namespace zzre
{
    public class OnceAction
    {
        public event Action Next = () => { };
        public void Invoke()
        {
            Next();
            Next = () => { };
        }
    }

    public class OnceAction<T1>
    {
        public event Action<T1> Next = (a) => { };
        public void Invoke(T1 a)
        {
            Next(a);
            Next = (a) => { };
        }
    }

    public class OnceAction<T1, T2>
    {
        public event Action<T1, T2> Next = (a, b) => { };
        public void Invoke(T1 a, T2 b)
        {
            Next(a, b);
            Next = (a, b) => { };
        }
    }

    public class OnceAction<T1, T2, T3>
    {
        public event Action<T1, T2, T3> Next = (a, b, c) => { };
        public void Invoke(T1 a, T2 b, T3 c)
        {
            Next(a, b, c);
            Next = (a, b, c) => { };
        }
    }
}
