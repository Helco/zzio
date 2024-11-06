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
    /// <summary>The current reference count to be atomically modified to keep assets alive or disposing them</summary>
    /// <remarks>Modifying has to be done using the <see cref="AddRef"/> and <see cref="DelRef"/> methods</remarks>
    int RefCount { get; }
    /// <summary>The *stored* apply actions to be taken after loading</summary>
    /// <remarks>This will not include immediate apply actions if the asset is loaded synchronously or was already loaded</remarks>
    OnceAction<AssetHandle> ApplyAction { get; }

    /// <summary>A lock to set asset state</summary>
    /// <remarks>Should never be held longer than necessary to set check and change asset state</remarks>
    SemaphoreSlim StateLock { get; }

    /// <summary>Starts the loading of the asset on the thread pool</summary>
    /// <remarks>This call is ignored if the <see cref="State"/> is not <see cref="AssetState.Queued"/></remarks>
    void StartLoading();
    /// <summary>Synchronously completes loading of the asset</summary>
    /// <remarks>This call will also rethrow loading exceptions</remarks>
    void Complete();
    /// <summary>Asynchronously completes loading of the asset</summary>
    /// <remarks>This call will also rethrow loading exceptions</remarks>
    Task CompleteAsync();
    /// <summary>Increases the reference count</summary>
    void AddRef();
    /// <summary>Decreases the reference count and disposes the asset if necessary</summary>
    /// <remarks>It will also signal a disposal to the registry</remarks>
    void DelRef();
    /// <summary>Rethrows a loading exception that already occured</summary>
    /// <remarks>Assumes that the load task has already completed with an error</remarks>
    void ThrowIfError();
    /// <summary>Unloads the asset and marks it as disposed</summary>
    /// <remarks>Can only be called while holding a lock</remarks>
    void Dispose();
}

public struct SemaphoreSlimScope(SemaphoreSlim semaphore) : IDisposable
{
    private bool wasReleased;

    public void Dispose()
    {
        if (!wasReleased)
        {
            semaphore.Release();
            wasReleased = true;
        }
    }
}

public static class SemaphoreSlimExtensions
{
    public static SemaphoreSlimScope Lock(this SemaphoreSlim @this)
    {
        @this.Wait();
        return new(@this);
    }

    public static Task<SemaphoreSlimScope> LockAsync(this SemaphoreSlim @this) =>
        @this.WaitAsync().ContinueWith(_ => new SemaphoreSlimScope(@this));
    
    public static SemaphoreSlimScope Lock(this SemaphoreSlim @this, CancellationToken ct)
    {
        @this.Wait(ct);
        return new(@this);
    }
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
    private readonly SemaphoreSlim stateLock = new(1, 1);
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
    SemaphoreSlim IAsset.StateLock => stateLock;

    void IAsset.Dispose()
    {
        Debug.Assert(stateLock.CurrentCount == 0);
        if (State is AssetState.Disposed or AssetState.Error)
            return;
        State = AssetState.Disposed;

        try
        {
            Unload();
        }
        finally
        {
            foreach (var handle in secondaryAssets)
                handle.Dispose();
            secondaryAssets = [];
        }
    }

    void IAsset.StartLoading()
    {
        Debug.Assert(stateLock.CurrentCount == 0);
        if (State != AssetState.Queued)
            return;
        State = AssetState.Loading;
        
        // If we came here we are responsible for setting State = Loading
        // so we are also responsible to actually start the loading.
        // Through Task.Run we enable that the caller of StartLoading releases the lock
        Task.Run(PrivateLoad, InternalRegistry.Cancellation);
    }

    private (bool shouldStartLoading, bool shouldWaitForCompletion) DecideCompletionActions()
    {
        Debug.Assert(stateLock.CurrentCount == 0);
        switch (State)
        {
            case AssetState.Loaded:
            case AssetState.Disposed:
            case AssetState.Error:
                return (false, false);

            case AssetState.Loading:
            case AssetState.LoadingSecondary:
                return (false, true);

            case AssetState.Queued:
                State = AssetState.Loading;
                return (true, true);

            default:
                throw new NotImplementedException($"Unimplemented asset state {State}");
        }
    }

    void IAsset.Complete()
    {
        bool shouldStartLoading = false;
        bool shouldWaitForCompletion = false;
        using (var _ = stateLock.Lock())
            (shouldStartLoading, shouldWaitForCompletion) = DecideCompletionActions();

        if (shouldStartLoading)
            Task.Run(PrivateLoad, InternalRegistry.Cancellation);
        if (shouldWaitForCompletion)
            InternalRegistry.WaitSynchronously(completionSource.Task);
        (this as IAsset).ThrowIfError();
    }

    async Task IAsset.CompleteAsync()
    {
        bool shouldStartLoading = false;
        bool shouldWaitForCompletion = false;
        using (var _ = await stateLock.LockAsync())
            (shouldStartLoading, shouldWaitForCompletion) = DecideCompletionActions();

        if (shouldStartLoading)
            await PrivateLoad();
        else if (shouldWaitForCompletion) // if we finished loading we do not need to wait for completion
            await completionSource.Task;
        (this as IAsset).ThrowIfError();
    }

    void IAsset.AddRef()
    {
        Debug.Assert(stateLock.CurrentCount == 0);
        Interlocked.Increment(ref refCount);
    }

    void IAsset.DelRef()
    {
        Debug.Assert(stateLock.CurrentCount == 0);
        if (--refCount != 0) // because we are locked, if we ever get negative
            return; // somebody else already should have disposed the asset

        try
        {
            (this as IAsset).Dispose();
        }
        finally
        {
            InternalRegistry.QueueRemoveAsset(this);
        }
    }

    private async Task<bool> CompareExchangeState(AssetState expected, AssetState next)
    {
        using (var _ = await stateLock.LockAsync())
        {
            if (State != expected)
                return false;
            State = next;
            return true;
        }
    }

    private async Task PrivateLoad()
    {
        if (!await CompareExchangeState(AssetState.Loading, AssetState.Loading))
            return;

        var ct = InternalRegistry.Cancellation;
        var expectedStateBeforeFinish = AssetState.Loading;
        try
        {
            var secondaryAssetSet = Load();
            if (ReferenceEquals(secondaryAssetSet, LoadAsynchronously))
                secondaryAssetSet = await LoadAsync();
            if (ReferenceEquals(secondaryAssetSet, LoadAsynchronously))
                throw new InvalidOperationException("LoadAsync is not allowed to return LoadAsynchronously");
            if (!ReferenceEquals(secondaryAssetSet, NoSecondaryAssets))
            {
                secondaryAssets = secondaryAssetSet.ToArray();
                EnsureLocality(secondaryAssets);
                ct.ThrowIfCancellationRequested();

                if (secondaryAssets.Length > 0 && NeedsSecondaryAssets)
                {
                    if (!await CompareExchangeState(AssetState.Loading, AssetState.LoadingSecondary))
                        return;
                    expectedStateBeforeFinish = AssetState.LoadingSecondary;
                    await InternalRegistry.WaitAsyncAll(secondaryAssets);
                }
            }

            ct.ThrowIfCancellationRequested();
            if (!await CompareExchangeState(expectedStateBeforeFinish, AssetState.Loaded))
                return;
            InternalRegistry.QueueApplyAsset(this);
            completionSource.SetResult(); // this might be the reason for unordered apply actions
        }
        catch (Exception ex)
        {
            await stateLock.WaitAsync();
            try
            {
                (this as IAsset).Dispose();
            }
            finally
            {
                State = AssetState.Error;
                stateLock.Release();
                completionSource.SetException(ex);
            }
        }
    }

    void IAsset.ThrowIfError()
    {
        var state = State;
        if (state == AssetState.Error)
        {
            var exception = completionSource.Task.Exception;
            if (exception is null)
                throw new InvalidOperationException("Asset was marked erroneous but does not contain exception");
            else
                ExceptionDispatchInfo.Capture(exception.InnerException!).Throw();
        }
#pragma warning disable CA1513 // Use ObjectDisposedException throw helper
                               // ThrowIf does not allow to set custom object name
        else if (state == AssetState.Disposed)
            throw new ObjectDisposedException(ToString());
#pragma warning restore CA1513 // Use ObjectDisposedException throw helper
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
