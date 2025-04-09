using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace zzre;

internal sealed class AssetApplyAction(CancellationToken cancellation) : IDisposable
{
    private readonly CancellationToken cancellation = cancellation;
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly OnceAction<AssetHandle> actions = new();

    public void Dispose()
    {
        Debug.Assert(cancellation.IsCancellationRequested);
        semaphore.Dispose();
        actions.Reset();
    }

    public void Add(Action<AssetHandle> action)
    {
        bool hasLock = false;
        try
        {
            semaphore.Wait(cancellation);
            hasLock = true;
            actions.Next += action;
        }
        catch(OperationCanceledException) {}
        catch(ObjectDisposedException) {}
        finally
        {
            if (hasLock)
                semaphore.Release();
        }
    }

    public async Task AddAsync(Action<AssetHandle> action)
    {
        bool hasLock = false;
        try
        {
            await semaphore.WaitAsync(cancellation);
            hasLock = true;
            actions.Next += action;
        }
        catch(OperationCanceledException) {}
        catch(ObjectDisposedException) {}
        finally
        {
            if (hasLock)
                semaphore.Release();
        }
    }
    
    public void Execute(AssetHandle handle)
    {
        Action<AssetHandle>? actions = null;

        bool hasLock = false;
        try
        {
            semaphore.Wait(cancellation);
            hasLock = true;
            actions = this.actions.Reset();
        }
        catch(OperationCanceledException) {}
        catch(ObjectDisposedException) {}
        finally
        {
            if (hasLock)
                semaphore.Release();
        }

        actions?.Invoke(handle);
    }
}
