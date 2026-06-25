using System;

namespace zzre;

public sealed class NullDisposable : IDisposable
{
    private NullDisposable() { }
    public static readonly NullDisposable Instance = new();
    public void Dispose() { }
}
