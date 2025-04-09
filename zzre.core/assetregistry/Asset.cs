using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace zzre;

/// <summary>The base class for asset types</summary>
public abstract class Asset : IAsset
{
    protected static readonly IEnumerable<AssetHandle> NoSecondaryAssets = [];

    public IAssetRegistry Registry => registry;
    protected ITagContainer diContainer => registry.DIContainer;

    private readonly IAssetRegistry registry;
    private readonly CancellationTokenSource cancellation;
    private readonly AssetApplyAction applyAction;
    private string? description;
    private AssetHandle[] secondaryAssets = [];
    private FFTask? loadingTask;
    private int stateInt, refCount;

    /// <inheritdoc/>
    public int RefCount => refCount;

    /// <inheritdoc/>
    public Guid ID { get; }

    /// <inheritdoc/>
    public AssetState State => (AssetState)stateInt;

    /// <inheritdoc/>
    public AssetLoadPriority Priority { get; private set; }

    public FFTask LoadTask => loadingTask!;

    /// <summary>The base class for asset types</summary>
    /// <remarks>Only call the constructor with data given by a registry</remarks>
    /// <param name="registry">The apparent registry of this asset to report and load secondary assets from</param>
    /// <param name="id">The ID chosen by the registry for this asset</param>
    public Asset(IAssetRegistry registry, Guid id)
    {
        this.registry = registry;
        ID = id;
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(registry.InternalRegistry.Cancellation);
        applyAction = new(cancellation.Token);
        loadingTask = new FFTask(PrivateLoad, cancellation.Token);
    }
    
    void IAsset.Dispose()
    {
        if (Interlocked.Exchange(ref stateInt, (int)AssetState.Disposed) == (int)AssetState.Disposed)
            return;
        cancellation.Cancel();

        Unload();

        applyAction.Dispose();
        foreach (var handle in secondaryAssets)
            handle.Dispose();
        secondaryAssets = [];
    }

    void IAsset.StartLoading(AssetLoadPriority priority)
    {
        if (!TryChangeState(AssetState.Queued, AssetState.Loading))
            return;
        Debug.Assert(loadingTask is null);
        Priority = priority;
        ;
    }

    // Due to FFTask, it is okay to throw in this method
    private async Task PrivateLoad()
    {
        Debug.Assert((AssetState)stateInt == AssetState.Loading);
        try
        {
            var secondaries = await LoadAsync(cancellation.Token);
            var prevExpectedState = AssetState.Loading;
            if (secondaries?.Any() is true)
            {
                PrepareSecondaryAssets(secondaries);
                cancellation.Token.ThrowIfCancellationRequested();

                if (NeedsSecondaryAssets)
                {
                    CheckedChangeState(AssetState.Loading, prevExpectedState = AssetState.LoadingSecondary);
                    await registry.WaitAsyncAll([.. secondaries], cancellation.Token);
                }
            }
            CheckedChangeState(prevExpectedState, AssetState.Loaded);
            registry.InternalRegistry.QueueApplyAsset(this);
        }
        catch
        {
            Interlocked.Exchange(ref stateInt, (int)AssetState.Error);
            try
            {
                (this as IAsset)!.Dispose();
            }
            finally {}
            throw;
        }
    }

    private void PrepareSecondaryAssets(IEnumerable<AssetHandle> secondaryAssetSet)
    {
        secondaryAssets = [.. secondaryAssetSet];
        foreach (ref var secondary in secondaryAssets.AsSpan())
        {
            if (!registry.InternalRegistry.IsLocalRegistry && secondary.registryInternal.IsLocalRegistry)
                throw new InvalidOperationException("Global assets cannot load local assets as secondary ones");
            secondary = new(secondary); // increments the reference count
        }
    }

    private void CheckedChangeState(AssetState expected, AssetState newState)
    {
        if (!TryChangeState(expected, newState))
        {
            const string Msg = "Asset state was changed to something non-final during load";
            Debug.Assert((AssetState)stateInt is AssetState.Error or AssetState.Disposed, Msg);
            throw new InvalidOperationException(Msg);
        }
        cancellation.Token.ThrowIfCancellationRequested();
    }

    private bool TryChangeState(AssetState expected, AssetState newState)
    {
        return Interlocked.CompareExchange(ref stateInt, (int)expected, (int)newState) == (int)expected;
    }

    ValueTask<FFTaskStatus> IAsset.Complete(AssetLoadPriority priority)
    {
        if (loadingTask is not null)
            return loadingTask.Completion;
        (this as IAsset).StartLoading(priority);
        Debug.Assert(loadingTask is not null);
        return loadingTask.Completion;
    }

    void IAsset.ThrowIfError()
    {
#pragma warning disable CA1513 // Use ObjectDisposedException throw helper
        if (State is AssetState.Error)
        {
            var exception = loadingTask?.Exception;
            if (exception is null)
                throw new InvalidOperationException("Asset was marked erroneous but does not contain exception");
            else
                ExceptionDispatchInfo.Throw(exception.InnerException ?? exception);
        }
        else if (State is AssetState.Disposed)
            throw new ObjectDisposedException(ToString());
#pragma warning restore CA1513 // Use ObjectDisposedException throw helper
    }

    void IAsset.AddRef()
    {
        var oldRefCount = Interlocked.Increment(ref refCount);
        if (oldRefCount < 0)
            Interlocked.Exchange(ref refCount, -1);
    }

    void IAsset.DelRef()
    {
        var oldRefCount = Interlocked.Decrement(ref refCount);
        if (oldRefCount > 1)
            return;
        Debug.Assert(oldRefCount == 1);
        Interlocked.Decrement(ref refCount); // now it is -1, signalling disposal

        (this as IAsset).Dispose();
        registry.InternalRegistry.QueueRemoveAsset(this);
    }

    void IAsset.AddApplyAction(Action<AssetHandle>? action)
    {
        if (action is not null)
            applyAction.Add(action);
    }

    Task IAsset.AddApplyActionAsyc(Action<AssetHandle>? action)
    {
        return action is null
            ? Task.CompletedTask
            : applyAction.AddAsync(action);
    }

    void IAsset.ExecuteApplyActions(AssetHandle handle)
    {
        applyAction.Execute(handle);
    }

    /// <summary>Whether marking this asset as loaded should be deferred until all secondary asset are loaded as well</summary>
    protected virtual bool NeedsSecondaryAssets { get; } = true;

    protected virtual IEnumerable<AssetHandle>? Load()
    {
        throw new NotImplementedException("Neither synchronous nor asynchronous loading was overridden");
    }

    /// <summary> Override this method to load the asset contents asynchronously.</summary>
    /// <returns>The set of secondary assets to be loaded from the same registry interface as this asset</returns>
    protected virtual async Task<IEnumerable<AssetHandle>?> LoadAsync(CancellationToken ct)
    {
        await Task.Yield();
        return Load();
    }

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
