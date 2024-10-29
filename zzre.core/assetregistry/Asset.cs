using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace zzre;

/// <summary>The loading state of an asset</summary>
public enum AssetState
{
    /// <summary>Loading of the asset has not been started yet</summary>
    /// <remarks>E.g. Low prioritised asset loading is started across several frames</remarks>
    Queued,
    /// <summary>The asset is currently loading</summary>
    Loading,
    /// <summary>The primary asset is loaded, but needs to wait for secondary asset loading to finish</summary>
    LoadingSecondary,
    /// <summary>The asset has been completly loaded and is ready to use</summary>
    /// <remarks>This state does not entail applying the asset</remarks>
    Loaded,
    /// <summary>The asset has been disposed of and should not be used anymore</summary>
    Disposed,
    /// <summary>There was some error during loading and the asset should not be used</summary>
    Error
}

/// <summary>The internal interface into an asset</summary>
internal interface IAsset : IDisposable
{
    Guid ID { get; }
    AssetState State { get; }
    /// <summary>The task communicates completion state and error handling during loading</summary>
    Task LoadTask { get; }
    /// <summary>The priority that the asset was *effectively* loaded with</summary>
    AssetLoadPriority Priority { get; set; }
    /// <summary>The current reference count to be atomically modified to keep assets alive or disposing them</summary>
    /// <remarks>Modifying has to be done using the <see cref="AddRef"/> and <see cref="DelRef"/> methods</remarks>
    int RefCount { get; }
    /// <summary>The *stored* apply actions to be taken after loading</summary>
    /// <remarks>This will not include immediate apply actions if the asset is loaded synchronously or was already loaded</remarks>
    OnceAction<AssetHandle> ApplyAction { get; }

    /// <summary>Starts the loading of the asset on the thread pool</summary>
    /// <remarks>This call is ignored if the <see cref="State"/> is not <see cref="AssetState.Queued"/></remarks>
    void StartLoading();
    /// <summary>Synchronously completes loading of the asset</summary>
    /// <remarks>This call will also rethrow loading exceptions</remarks>
    void Complete();
    /// <summary>Atomically increases the reference count</summary>
    void AddRef();
    /// <summary>Atomically decreases the reference count and disposes the asset if necessary</summary>
    /// <remarks>It will also signal a disposal to the registry</remarks>
    void DelRef();
    /// <summary>Rethrows a loading exception that already occured</summary>
    /// <remarks>Assumes that the load task has already completed with an error</remarks>
    void ThrowIfError();
}

/// <summary>The base class for asset types</summary>
/// <remarks>Only call the constructor with data given by a registry</remarks>
/// <param name="registry">The apparent registry of this asset to report and load secondary assets from</param>
/// <param name="id">The ID chosen by the registry for this asset</param>
public abstract class Asset(IAssetRegistry registry, Guid id) : IAsset
{
    protected static ValueTask<IEnumerable<AssetHandle>> NoSecondaryAssets =>
        ValueTask.FromResult(Enumerable.Empty<AssetHandle>());

    /// <summary>The <see cref="ITagContainer"/> of the apparent registry to be used during loading</summary>
    protected readonly ITagContainer diContainer = registry.DIContainer;
    private readonly TaskCompletionSource completionSource = new();
    private string? description;
    private AssetHandle[] secondaryAssets = [];
    private int refCount;

    private IAssetRegistryInternal InternalRegistry { get; } = registry.InternalRegistry;
    public IAssetRegistry Registry { get; } = registry;
    /// <summary>An unique identifier given by the registry related to the Info value in order to efficiently address an asset instance</summary>
    /// <remarks>The ID is chosen randomly and will change at least per process per asset</remarks>
    public Guid ID { get; } = id;
    /// <summary>The current loading state of the asset</summary>
    public AssetState State { get; private set; }
    Task IAsset.LoadTask => completionSource.Task;
    int IAsset.RefCount => refCount;
    AssetLoadPriority IAsset.Priority { get; set; }
    OnceAction<AssetHandle> IAsset.ApplyAction { get; } = new();

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
                    (this as IAsset).ThrowIfError();
                    return;

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
                InternalRegistry.QueueRemoveAsset(this);
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
            InternalRegistry.QueueApplyAsset(this);
        }
        catch (Exception ex)
        {
            lock (this)
            {
                State = AssetState.Error;
                completionSource.SetException(ex);
                (this as IDisposable).Dispose();
            }
        }
    }

    void IAsset.ThrowIfError()
    {
        if (State == AssetState.Error)
        {
            completionSource.Task.WaitAndRethrow();
            throw new InvalidOperationException("Asset was marked erroneous but does not contain exception");
        }
    }

    [Conditional("DEBUG")]
    private void EnsureLocality(AssetHandle[] secondaryAssets)
    {
        foreach (var secondary in secondaryAssets)
            if (!InternalRegistry.IsLocalRegistry && secondary.registryInternal.IsLocalRegistry)
                throw new InvalidOperationException("Global assets cannot load local assets as secondary ones");
    }

    /// <summary>Whether marking this asset as loaded should be deferred until all secondary asset are loaded as well</summary>
    protected virtual bool NeedsSecondaryAssets { get; } = true;
    /// <summary>Override this method to actually load the asset contents</summary>
    /// <remarks>This method can be called asynchronously</remarks>
    /// <returns>The set of secondary assets to be loaded from the same registry interface as this asset</returns>
    protected abstract ValueTask<IEnumerable<AssetHandle>> Load();
    /// <summary>Unloads any resources the asset might hold</summary>
    /// <remarks>It is not necessary to manually dispose secondary asset handles</remarks>
    protected abstract void Unload();

    /// <summary>Produces a description of the asset to be shown in debug logs and tools</summary>
    /// <returns>A description of the asset instance for debugging</returns>
    public sealed override string ToString() => description ??= ToStringInner();

    /// <summary>Produces a description of the asset to be shown in debug logs and tools</summary>
    /// <remarks>Used to cache description strings</remarks>
    /// <returns>A description of the asset instance for debugging</returns>
    protected virtual string ToStringInner() => $"{GetType().Name} {ID}";
}
