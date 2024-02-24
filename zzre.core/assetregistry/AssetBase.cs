using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TerraFX.Interop.Vulkan;

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

    void StartLoading();
    void Complete();
    void AddRef();
    void DelRef();
}

public abstract class AssetBase<TValue> : IAsset where TValue : class
{
    private readonly TaskCompletionSource completionSource = new();
    private AssetHandle[] secondaryAssets = [];
    private TValue? value;
    private int refCount;

    private IAssetRegistry InternalRegistry => Registry;
    public AssetRegistry Registry { get; }
    public Guid ID { get; }
    public AssetState State { get; private set; }
    public TValue? Value => State == AssetState.Loaded ? value : null;
    Task IAsset.LoadTask => completionSource.Task;

    public AssetBase(AssetRegistry registry, Guid id)
    {
        Registry = registry;
        ID = id;
    }

    void IDisposable.Dispose()
    {
        if (State != AssetState.Error)
            State = AssetState.Disposed;

        foreach (var handle in secondaryAssets)
            handle.Dispose();
        secondaryAssets = [];

        if (value != null)
            Unload();
        value = null;
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
            (this as IAsset).Dispose();
            InternalRegistry.QueueRemoveAsset(this).AsTask().Wait();
        }
    }

    private async Task PrivateLoad()
    {
        try
        {
            IEnumerable<AssetHandle> secondaryAssetSet;
            (value, secondaryAssetSet) = await Load();
            secondaryAssets = secondaryAssetSet.ToArray();

            if (secondaryAssets.Length > 0)
            {
                lock (this)
                {
                    State = AssetState.LoadingSecondary;
                }
                await InternalRegistry.WaitAsyncAll(secondaryAssets);
            }

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

    protected abstract ValueTask<(TValue value, IEnumerable<AssetHandle> secondaryAssets)> Load();
    protected abstract void Unload();
}
