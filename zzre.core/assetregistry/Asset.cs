using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
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
internal interface IAsset
{
    Guid ID { get; }
    AssetState State { get; }
    /// <summary>The task communicates completion state and error handling during loading</summary>
    Task LoadTask { get; }
    /// <summary>The priority that the asset was *effectively* loaded with</summary>
    AssetLoadPriority Priority { get; set; }
    /// <summary>The current reference count to be modified to keep assets alive or disposing them</summary>
    /// <remarks>Modifying has to be done using the <see cref="AddRef"/> and <see cref="DelRef"/> methods</remarks>
    int RefCount { get; }
    /// <summary>The *stored* apply actions to be taken after loading</summary>
    /// <remarks>This will not include immediate apply actions if the asset is loaded synchronously or was already loaded</remarks>
    OnceAction<AssetHandle> ApplyAction { get; }

    SemaphoreSlim StateLock { get; }

    /// <summary>Starts the loading of the asset on the thread pool</summary>
    /// <remarks>This call is ignored if the <see cref="State"/> is not <see cref="AssetState.Queued"/></remarks>
    void StartLoading();
    /// <summary>Increases the reference count</summary>
    void AddRef();
    /// <summary>Decreases the reference count and disposes the asset if necessary</summary>
    /// <remarks>It will also signal a disposal to the registry</remarks>
    void DelRef();
    /// <summary>Rethrows a loading exception that already occured</summary>
    /// <remarks>Assumes that the load task has already completed with an error</remarks>
    void ThrowIfError();
    void Dispose();
}

/// <summary>The base class for asset types</summary>
/// <remarks>Only call the constructor with data given by a registry</remarks>
/// <param name="registry">The apparent registry of this asset to report and load secondary assets from</param>
/// <param name="id">The ID chosen by the registry for this asset</param>
public abstract class Asset(IAssetRegistry registry, Guid id) : IAsset
{
    private sealed class LoadAsynchronousSentinel : IEnumerable<AssetHandle>
    {
        public IEnumerator<AssetHandle> GetEnumerator() =>
            throw new InvalidOperationException("This is a sentinel value, you cannot use this for anything other than ReferenceEquals");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    protected static readonly IEnumerable<AssetHandle> NoSecondaryAssets = [];
    protected static readonly IEnumerable<AssetHandle> LoadAsynchronously = new LoadAsynchronousSentinel();

    /// <summary>The <see cref="ITagContainer"/> of the apparent registry to be used during loading</summary>
    protected readonly ITagContainer diContainer = registry.DIContainer;
    private readonly TaskCompletionSource completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
    SemaphoreSlim IAsset.StateLock { get; } = new(1, 1);

    void IAsset.Dispose()
    {
        Debug.Assert((this as IAsset).StateLock.CurrentCount == 0);
        if (State is AssetState.Disposed or AssetState.Error)
            return;
        State = AssetState.Disposed;

        Unload();

        foreach (var handle in secondaryAssets)
            handle.Dispose();
        secondaryAssets = [];
    }

    void IAsset.StartLoading()
    {
        Debug.Assert((this as IAsset).StateLock.CurrentCount == 0 && State == AssetState.Queued);
        State = AssetState.Loading;
        Task.Run(PrivateLoad, InternalRegistry.Cancellation);
    }

    void IAsset.AddRef()
    {
        Debug.Assert((this as IAsset).StateLock.CurrentCount == 0);
        refCount++;
    }

    void IAsset.DelRef()
    {
        Debug.Assert((this as IAsset).StateLock.CurrentCount == 0);
        if (--refCount != 0)
            return;

        (this as IAsset).Dispose();
        InternalRegistry.QueueRemoveAsset(this);
    }

    private async Task PrivateLoad()
    {
        // TODO: This will currently not work when disposed while loading

        if (State != AssetState.Loading)
            throw new InvalidOperationException("Asset.PrivateLoad was called during an unexpected state");

        var ct = InternalRegistry.Cancellation;
        try
        {
            var secondaryAssetSet = Load();
            if (ReferenceEquals(secondaryAssetSet, LoadAsynchronously))
                secondaryAssetSet = await LoadAsync();
            if (ReferenceEquals(secondaryAssetSet, LoadAsynchronously))
                throw new InvalidOperationException("LoadAsync is not allowed to return LoadAsynchronously");
            if (!ReferenceEquals(secondaryAssetSet, NoSecondaryAssets))
            {
                PrepareSecondaryAssets(secondaryAssetSet);
                ct.ThrowIfCancellationRequested();

                if (secondaryAssets.Length > 0 && NeedsSecondaryAssets)
                {
                    (this as IAsset).StateLock.Wait();
                    try
                    {
                        Debug.Assert(State == AssetState.Loading);
                        State = AssetState.LoadingSecondary;
                    }
                    finally
                    {
                        (this as IAsset).StateLock.Release();
                    }
                    await InternalRegistry.WaitAsyncAll(secondaryAssets);
                }
            }

            ct.ThrowIfCancellationRequested();
            State = AssetState.Loaded;
            InternalRegistry.QueueApplyAsset(this);
            completionSource.SetResult();
        }
        catch (Exception ex)
        {
            (this as IAsset).StateLock.Wait();
            try
            {
                completionSource.SetException(ex);
                (this as IAsset).Dispose();
            }
            finally
            {
                (this as IAsset).StateLock.Release();
                State = AssetState.Error;
            }
        }
    }

    void IAsset.ThrowIfError()
    {
#pragma warning disable CA1513 // Use ObjectDisposedException throw helper
        if (State is AssetState.Error)
        {
            var exception = completionSource.Task.Exception;
            if (exception is null)
                throw new InvalidOperationException("Asset was marked erroneous but does not contain exception");
            else
                ExceptionDispatchInfo.Throw(exception.InnerException ?? exception);
        }
        else if (State is AssetState.Disposed)
            throw new ObjectDisposedException(ToString());
#pragma warning restore CA1513 // Use ObjectDisposedException throw helper
    }

    private void PrepareSecondaryAssets(IEnumerable<AssetHandle> secondaryAssetSet)
    {
        secondaryAssets = secondaryAssetSet.ToArray();
        foreach (ref var secondary in secondaryAssets.AsSpan())
        {
            if (!InternalRegistry.IsLocalRegistry && secondary.registryInternal.IsLocalRegistry)
                throw new InvalidOperationException("Global assets cannot load local assets as secondary ones");
            secondary = new(secondary); // increments the reference count
        }
    }

    /// <summary>Whether marking this asset as loaded should be deferred until all secondary asset are loaded as well</summary>
    protected virtual bool NeedsSecondaryAssets { get; } = true;
    /// <summary>Override this method to load the asset contents synchronously.</summary>
    /// <remarks>This method can be called asynchronously</remarks>
    /// <returns>The set of secondary assets to be loaded from the same registry interface as this asset or <see cref="LoadAsynchronously"/></returns>
    protected virtual IEnumerable<AssetHandle> Load() => LoadAsynchronously;
    /// <summary> Override this method to load the asset contents asynchronously.</summary>
    /// <remarks>This method is only used if <see cref="Load"/> returns <see cref="LoadAsynchronously"/></remarks>
    /// <returns>The set of secondary assets to be loaded from the same registry interface as this asset</returns>
    protected virtual Task<IEnumerable<AssetHandle>> LoadAsync() =>
        throw new NotImplementedException("Asset.Load returned LoadAsynchronously but LoadAsync was not implemented");
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
