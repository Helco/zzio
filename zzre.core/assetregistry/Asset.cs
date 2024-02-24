using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace zzre;

public enum AssetState
{
    Queued,
    Loading,
    LoadingSecondary,
    Loaded,
    Disposed,
    Error
}

internal interface IAsset : IDisposable
{
    Guid ID { get; }
    AssetState State { get; }
    Task LoadTask { get; }
    AssetLoadPriority Priority { get; set; }
    Action<AssetHandle>? ApplyAction { get; set; }

    void StartLoading();
    void Complete();
    void AddRef();
    void DelRef();
}

public abstract class Asset : IAsset
{
    private readonly TaskCompletionSource completionSource = new();
    private AssetHandle[] secondaryAssets = [];
    private int refCount;

    private IAssetRegistry InternalRegistry => Registry;
    public AssetRegistry Registry { get; }
    public Guid ID { get; }
    public AssetState State { get; private set; }
    Task IAsset.LoadTask => completionSource.Task;
    AssetLoadPriority IAsset.Priority { get; set; }
    Action<AssetHandle>? IAsset.ApplyAction { get; set; }

    public Asset(AssetRegistry registry, Guid id)
    {
        Registry = registry;
        ID = id;
    }

    void IDisposable.Dispose()
    {
        if (State != AssetState.Error)
            State = AssetState.Disposed;

        Unload();

        foreach (var handle in secondaryAssets)
            handle.Dispose();
        secondaryAssets = [];
    }

    void IAsset.StartLoading()
    {
        lock (this)
        {
            if (State != AssetState.Queued)
                return;
            State = AssetState.Loading;
            Task.Run(PrivateLoad, Registry.Cancellation);
        }
    }

    void IAsset.Complete()
    {

    }

    void IAsset.AddRef() => Interlocked.Increment(ref refCount);
    void IAsset.DelRef()
    {
        int oldRefCount;
        while (true)
        {
            oldRefCount = refCount;
            if (oldRefCount <= 0)
                return;
            if (Interlocked.CompareExchange(ref refCount, oldRefCount - 1, oldRefCount) == oldRefCount)
                break;
        }
        if (oldRefCount == 1) // we just hit zero
        {
            lock (this)
            {
                (this as IAsset).Dispose();
                InternalRegistry.QueueRemoveAsset(this).AsTask().WaitAndRethrow();
            }
        }
    }

    private async Task PrivateLoad()
    {
        var ct = Registry.Cancellation;
        try
        {
            var secondaryAssetSet = await Load();
            secondaryAssets = secondaryAssetSet.ToArray();
            ct.ThrowIfCancellationRequested();

            if (secondaryAssets.Length > 0)
            {
                lock (this)
                {
                    State = AssetState.LoadingSecondary;
                }
                await InternalRegistry.WaitAsyncAll(secondaryAssets);
            }

            ct.ThrowIfCancellationRequested();
            await InternalRegistry.QueueApplyAsset(this);
            completionSource.SetResult();
        }
        catch (Exception ex)
        {
            lock(this)
            {
                State = AssetState.Error;
                completionSource.SetException(ex);
                (this as IDisposable).Dispose();
            }
        }
    }

    protected abstract ValueTask<IEnumerable<AssetHandle>> Load();
    protected abstract void Unload();

    public override string ToString() => $"Asset {ID}";
}
