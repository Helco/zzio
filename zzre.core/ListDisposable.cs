using System;
using System.Collections.Generic;

namespace zzre
{
    public class ListDisposable : BaseDisposable
    {
        private readonly List<WeakReference<IDisposable>> disposables = new List<WeakReference<IDisposable>>();

        protected override void DisposeManaged()
        {
            foreach (var weakDisposable in disposables)
            {
                if (weakDisposable.TryGetTarget(out var disposable))
                    disposable.Dispose();
            }
            disposables.Clear();
        }

        protected void AddDisposable(IDisposable disposable) => disposables.Add(new WeakReference<IDisposable>(disposable));
    }
}
