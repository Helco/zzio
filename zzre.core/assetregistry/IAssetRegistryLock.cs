using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace zzre;

public interface IAssetRegistryLock : IDisposable
{
    Releaser Wait(TimeSpan timeout, CancellationToken ct, [CallerMemberName] string context = "<unknown>");
    Task<Releaser> WaitAsync(TimeSpan timeout, CancellationToken ct, [CallerMemberName] string context = "<unknown>");

    internal void Release();
    public struct Releaser(IAssetRegistryLock? parent) : IDisposable
    {
        private IAssetRegistryLock? parent = parent;
        public void Dispose()
        {
            parent?.Release();
            parent = null;
        }
        public static implicit operator bool(in Releaser l) => l.parent is not null;
        public static bool operator true(in Releaser l) => l.parent is not null;
        public static bool operator false(in Releaser l) => l.parent is null;

        public static Releaser ContinueBoolTask(Task<bool> task, object? parent)
            => task.Result ? new(parent as IAssetRegistryLock) : default;

        public static Task<Releaser> ConvertFromBoolTask(Task<bool> task, IAssetRegistryLock l, CancellationToken ct) =>
            task.ContinueWith(ContinueBoolTask, l, ct, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Current);
    }
}

public sealed class SemaphoreAssetLock : IAssetRegistryLock
{
    private readonly SemaphoreSlim semaphore = new(1, 1);

    public void Dispose() => semaphore.Dispose();

    void IAssetRegistryLock.Release() => semaphore.Release();

    public IAssetRegistryLock.Releaser Wait(TimeSpan timeout, CancellationToken ct, string _) =>
        semaphore.Wait(timeout, ct) ? new(this) : default;

    public Task<IAssetRegistryLock.Releaser> WaitAsync(TimeSpan timeout, CancellationToken ct, string _) =>
        IAssetRegistryLock.Releaser.ConvertFromBoolTask(semaphore.WaitAsync(timeout, ct), this, ct);
}

public sealed class TrackingAssetLock(IAssetRegistryLock inner) : IAssetRegistryLock
{
    private string? last;

    public string? Last => last;

    public void Dispose()
    {
        inner.Dispose();
    }

    void IAssetRegistryLock.Release()
    {
        inner.Release();
        Interlocked.Exchange(ref last, null);
        last = null;
    }

    public IAssetRegistryLock.Releaser Wait(TimeSpan timeout, CancellationToken ct, string context)
    {
        var releaser = inner.Wait(timeout, ct, context);
        try
        {
            if (releaser)
                Interlocked.Exchange(ref last, context);
        }
        catch
        {
            Debug.Assert(false);
            releaser.Dispose();
            throw;
        }
        return releaser;
    }

    public async Task<IAssetRegistryLock.Releaser> WaitAsync(TimeSpan timeout, CancellationToken ct, string context)
    {
        var releaser = await inner.WaitAsync(timeout, ct, context);
        try
        {
            if (releaser)
                Interlocked.Exchange(ref last, context);
        }
        catch
        {
            Debug.Assert(false);
            releaser.Dispose();
            throw;
        }
        return releaser;
    }
}
