using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace zzre
{
    public class ResettableLazy<T> : BaseDisposable where T : class
    {
        private readonly bool isThreadSafe;
        private readonly Func<T> creator;
        private T? value;

        public T Value
        {
            get
            {
                if (isThreadSafe)
                {
                    lock (this)
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
            this.isThreadSafe = isThreadSafe;
            value = initialValue;
        }

        public void Reset()
        {
            if (isThreadSafe)
            {
                lock (this)
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
        private readonly bool isThreadSafe;
        private readonly Func<T> creator;
        private T? value;

        public T Value
        {
            get
            {
                if (isThreadSafe)
                {
                    lock (this)
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
            this.isThreadSafe = isThreadSafe;
            value = initialValue;
        }

        public void Reset()
        {
            if (isThreadSafe)
            {
                lock (this)
                {
                    value = null;
                }
            }
            else
                value = null;
        }

        public static implicit operator T(ResettableLazyValue<T> lazy) => lazy.Value;
    }
}
