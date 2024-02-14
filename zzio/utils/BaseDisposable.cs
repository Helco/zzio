using System;

namespace zzio;

// see https://docs.microsoft.com/de-de/visualstudio/code-quality/ca1063?view=vs-2019
public class BaseDisposable : IDisposable
{
    private bool isDisposed;

    ~BaseDisposable() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (isDisposed)
            return;
        isDisposed = true;
        if (disposing)
            DisposeManaged();
        DisposeNative();
    }

    protected virtual void DisposeManaged() { }
    protected virtual void DisposeNative() { }
}
