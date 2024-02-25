using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    OnceAction<AssetHandle> ApplyAction { get; }

    void StartLoading();
    void Complete();
    void AddRef();
    void DelRef();
}

public abstract class Asset : IAsset
{
    protected static ValueTask<IEnumerable<AssetHandle>> NoSecondaryAssets =>
        ValueTask.FromResult(Enumerable.Empty<AssetHandle>());

    protected readonly ITagContainer diContainer;
    private readonly TaskCompletionSource completionSource = new();
    private AssetHandle[] secondaryAssets = [];
    private int refCount;

    private IAssetRegistryInternal InternalRegistry { get; }
    public IAssetRegistry Registry { get; }
    public Guid ID { get; }
    public AssetState State { get; private set; }
    Task IAsset.LoadTask => completionSource.Task;
    AssetLoadPriority IAsset.Priority { get; set; }
    OnceAction<AssetHandle> IAsset.ApplyAction { get; } = new();

    public Asset(IAssetRegistry registry, Guid id)
    {
        Registry = registry;
        InternalRegistry = registry.InternalRegistry;
        diContainer = registry.DIContainer;
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
            Task.Run(PrivateLoad, InternalRegistry.Cancellation);
        }
    }

    void IAsset.Complete()
    {
        lock (this)
        {
            switch (State)
            {
                case AssetState.Loaded: return;

                case AssetState.Loading:
                case AssetState.LoadingSecondary:
                    completionSource.Task.WaitAndRethrow();
                    return;

                case AssetState.Queued:
                    State = AssetState.Loading;
                    PrivateLoad().WaitAndRethrow();
                    return;

                case AssetState.Disposed:
                    throw new ObjectDisposedException(ToString());
                case AssetState.Error:
                    completionSource.Task.WaitAndRethrow();
                    throw new InvalidOperationException("Asset was marked erroneous but does not contain exception");

                default:
                    throw new NotImplementedException($"Unimplemented asset state {State}");
            }
        }
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
        if (State != AssetState.Loading)
            throw new InvalidOperationException("Asset.PrivateLoad was called during an unexpected state");

        var ct = InternalRegistry.Cancellation;
        try
        {
            var secondaryAssetSet = await Load();
            secondaryAssets = secondaryAssetSet.ToArray();
            EnsureLocality(secondaryAssets);
            ct.ThrowIfCancellationRequested();

            if (secondaryAssets.Length > 0 && NeedsSecondaryAssets)
            {
                lock (this)
                {
                    State = AssetState.LoadingSecondary;
                }
                await InternalRegistry.WaitAsyncAll(secondaryAssets);
            }

            ct.ThrowIfCancellationRequested();
            State = AssetState.Loaded;
            completionSource.SetResult();
            await InternalRegistry.QueueApplyAsset(this);
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

    [Conditional("DEBUG")]
    private void EnsureLocality(AssetHandle[] secondaryAssets)
    {
        foreach (var secondary in secondaryAssets)
            if (!InternalRegistry.IsLocalRegistry && secondary.registryInternal.IsLocalRegistry)
                throw new InvalidOperationException("Global assets cannot load local assets as secondary ones");
    }

    protected virtual bool NeedsSecondaryAssets { get; } = true;
    protected abstract ValueTask<IEnumerable<AssetHandle>> Load();
    protected abstract void Unload();

    public override string ToString() => $"{GetType().Name} {ID}";
}
