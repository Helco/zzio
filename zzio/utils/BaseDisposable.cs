using System;

namespace zzio;

// see https://docs.microsoft.com/de-de/visualstudio/code-quality/ca1063?view=vs-2019
public class BaseDisposable : IDisposable
{
    public bool WasDisposed { get; private set; }

    ~BaseDisposable() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (WasDisposed)
            return;
        WasDisposed = true;
        if (disposing)
            DisposeManaged();
        DisposeNative();
    }

    protected virtual void DisposeManaged() { }
    protected virtual void DisposeNative() { }
}
