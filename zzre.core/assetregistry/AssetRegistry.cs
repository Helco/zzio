using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DotNext.Threading.Tasks;
using Serilog;
using Serilog.Core;

namespace zzre;

internal sealed class AssetState
{
    public required bool NeedsMainThreadDisposal;
    public required TaskCompletionSource<IDisposable> LoadLazy;
    public required Func<Task> Factory;
    public required uint Tag; // used for apply actions to prevent triggering from revived assets
    public required Type AssetType;
    public IDisposable? Asset
    {
        get
        {
            var result = LoadLazy.Task.TryGetResult();
            return result is null || !result.Value.IsSuccessful
                ? null : result.Value.Value;
        }
    }
    public bool WasStarted;
    public int RefCount = 1;
    public AssetPriority Priority;
    public string? Name;
}

public class AssetRegistry : IAssetRegistryInternal
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(3);
    private static readonly int MaxLowPriorityAssetsPerFrame = Math.Clamp(Environment.ProcessorCount, 1, 64);
    private static readonly UnboundedChannelOptions ChannelOptions = new()
    {
        AllowSynchronousContinuations = true,
        SingleReader = true,
        SingleWriter = false
    };

    private readonly Channel<Guid> assetsToStart = Channel.CreateUnbounded<Guid>(ChannelOptions);
    private readonly Channel<IDisposable> assetsToDispose = Channel.CreateUnbounded<IDisposable>(ChannelOptions);
    private readonly Dictionary<Guid, AssetState> assets = [];
    private readonly CancellationTokenSource cancellationSource = new();
    private readonly IAssetRegistryLock mainLock = new TrackingAssetLock(new DotNextAsyncAssetLock());
    private readonly ILogger logger;
    private readonly int mainThreadId;
    private readonly Dictionary<Type, Action<Guid, object>> applyActionCaster = [];
    private List<(Guid assetId, uint tag, Type assetType, object action)> applyActions = [], applyActionsBackup = [];
    private uint nextAssetTag;
    private AssetRegistryStats localStats;

    public bool WasDisposed => cancellationSource.IsCancellationRequested;
    public bool IsMainThread => mainThreadId == Environment.CurrentManagedThreadId;
    public bool IsLocalRegistry => ParentRegistry is not null;
    public IAssetRegistry? ParentRegistry { get; }
    public CancellationToken Cancellation => cancellationSource.Token;
    public ITagContainer DIContainer { get; }
    public AssetRegistryStats Stats => (ParentRegistry?.Stats ?? default) + localStats;

    private static readonly TaskCompletionSource<IDisposable> DisposedSource;
    static AssetRegistry()
    {
        DisposedSource = new();
        DisposedSource.SetException(new ObjectDisposedException("NullDisposedSource"));
    }

    public AssetRegistry(ITagContainer diContainer, IAssetRegistry? parent = null, string? debugName = null)
    {
        DIContainer = diContainer;
        ObjectDisposedException.ThrowIf(parent is { WasDisposed: true }, typeof(AssetRegistry));
        if (parent is { IsLocalRegistry: true })
            throw new ArgumentException("Cannot use a local registry as parent");
        ParentRegistry = parent;
        logger = // ILogger is optional, as well as the log prefix
            !diContainer.TryGetTag<ILogger>(out var parentLogger) ? Logger.None
            : string.IsNullOrEmpty(debugName) ? diContainer.GetLoggerFor<AssetRegistry>()
            : diContainer.GetTag<ILogger>().For($"{nameof(AssetRegistry)}-{debugName}");
        mainThreadId = Environment.CurrentManagedThreadId;
    }

    public void Dispose()
    {
        if (WasDisposed)
            return;
        if (mainLock.Wait(LockTimeout, Cancellation))
            logger.Warning("AssetRegistry could not lock during dispose, going ahead nonetheless");
        cancellationSource.Cancel();
        foreach (var asset in assets.Values)
        {
            DisposeAssetState(asset);
        }
        assets.Clear();
        applyActions.Clear();
        applyActionsBackup.Clear();
        assetsToDispose.Writer.TryComplete();
        assetsToStart.Writer.TryComplete();
        DisposeOldAssets(); // after current assets in case we add something into it (we shouldn't)

        mainLock.Dispose();
        cancellationSource.Dispose();
        logger.Verbose("Finished disposing registry");
    }

    private void DisposeAssetObject(bool needsMainThreadDisposal, IDisposable? assetObject)
    {
        if (assetObject is null)
            return;
        if (IsMainThread || !needsMainThreadDisposal)
        {
            localStats.OnAssetRemoved();
            assetObject.Dispose();
        }
        else
        {
            var success = assetsToDispose.Writer.TryWrite(assetObject);
            Debug.Assert(success);
        }
    }

    private void DisposeAssetState(AssetState state)
    {
        DisposeAssetObject(state.NeedsMainThreadDisposal, state.Asset);
        state.RefCount = 0;
        state.LoadLazy = DisposedSource;
        state.WasStarted = false;
    }

    [ExcludeFromCodeCoverage] // we cannot reasonably check for semaphore failure
    private IAssetRegistryLock.Releaser LockSemaphore([CallerMemberName] string context = "<unknown>")
    {
        var releaser = mainLock.Wait(LockTimeout, Cancellation, context);
        if (!releaser)
            throw new InvalidOperationException("Could not lock asset registry");
        return releaser;
        // this should only happen in bug scenarios
    }

    [ExcludeFromCodeCoverage]
    private async Task<IAssetRegistryLock.Releaser> LockSemaphoreAsync(CancellationToken ct, [CallerMemberName] string context = "<unknown>")
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Cancellation, ct);
        var releaser = await mainLock.WaitAsync(LockTimeout, cts.Token, context);
        if (!releaser)
            throw new InvalidOperationException("Could not lock asset registry");
        return releaser;
    }

    void IAssetRegistryInternal.AddRef(Guid assetId)
    {
        ObjectDisposedException.ThrowIf(WasDisposed, typeof(IAssetRegistry));
        using var _ = LockSemaphore();
        ObjectDisposedException.ThrowIf(!TryAddRefUnsafe(assetId), typeof(IAsset));
    }

    private bool TryAddRefUnsafe(Guid assetId)
    {
        Debug.Assert(!WasDisposed);
        //Debug.Assert(semaphore.CurrentCount == 0);
        var assetState = assets.GetValueOrDefault(assetId);
        if (assetState is null || assetState.RefCount <= 0)
            return false;
        assetState.RefCount++;
        return true;
    }

    [ExcludeFromCodeCoverage]
    void IAssetRegistryInternal.DelRef(Guid assetId)
    {
        if (WasDisposed) return; // Ignore out-of-order deletion, all assets are already dead
        using var releaser = LockSemaphore();
        DelRefUnsafe(assetId);
    }

    private void DelRefUnsafe(Guid assetId)
    {
        Debug.Assert(!WasDisposed);
        //Debug.Assert(semaphore.CurrentCount == 0);
        var assetState = assets.GetValueOrDefault(assetId);
        if (assetState is null || assetState.RefCount <= 0)
            return; // Let's just ignore already-dead assets, we got what we wanted
        if (--assetState.RefCount <= 0)
        {
            DisposeAssetState(assetState);
            assets.Remove(assetId);
        }
    }

    TaskCompletionSource<IDisposable> IAssetRegistryInternal.GetAsset(Guid assetId)
    {
        AssetState asset;
        using var releaser = LockSemaphore();
        ObjectDisposedException.ThrowIf(!assets.TryGetValue(assetId, out asset!), nameof(IAssetHandle));
        if (!asset.WasStarted)
        {
            Task.Run(() => TryStartLoad(assetId), Cancellation);
            asset.WasStarted = true;
        }
        return asset.LoadLazy;
    }

    void IAssetRegistryInternal.CheckType(Guid assetId, Type type)
    {
        using var releaser = LockSemaphore();
        ObjectDisposedException.ThrowIf(!assets.TryGetValue(assetId, out var asset) || asset.RefCount <= 0, nameof(IAssetHandle));
        if (!asset.AssetType.IsAssignableTo(type))
            throw new InvalidCastException($"Cannot cast asset of type {asset.AssetType.FullName} to {type.FullName}");
    }

    public AssetHandle<TAsset> Load<TInfo, TAsset>(in TInfo info, AssetPriority priority)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IAsset<TInfo>
    {
        ObjectDisposedException.ThrowIf(WasDisposed, typeof(IAssetRegistry));
        if (TAsset.Locality is not AssetLocality.Global && !IsLocalRegistry)
            throw new ArgumentException($"Cannot load a local asset {typeof(TAsset).FullName} from a global registry");
        if (TAsset.Locality is AssetLocality.Global && IsLocalRegistry)
            return ParentRegistry!.Load<TInfo, TAsset>(info, priority);

        var (assetId, assetState) = GetOrCreateAssetState<TInfo, TAsset>(info, priority);
        var handle = new AssetHandle<TAsset>(this, assetId);
        if (!assetState.LoadLazy.Task.IsCompleted)
        {
            switch (priority)
            {
                case AssetPriority.Synchronous:
                    try
                    {
                        handle.Get(); // checks main thread
                    }
                    catch
                    {
                        // the user does not get the handle, so there shouldn't be a refcount on the asset
                        (this as IAssetRegistryInternal).DelRef(assetId);
                        throw;
                    }
                    break;
                case AssetPriority.High:
                    Task.Run(() => TryStartLoad(handle.AssetId), Cancellation);
                    assetState.WasStarted = true;
                    break;
                case AssetPriority.Low:
                    var success = assetsToStart.Writer.TryWrite(assetId);
                    Debug.Assert(success); // As the channel is unbounded it should never fail to write
                    break;
            }
        }
        return handle;
    }

    private (Guid, AssetState) GetOrCreateAssetState<TInfo, TAsset>(in TInfo info, AssetPriority priority)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IAsset<TInfo>
    {
        using var releaser = LockSemaphore();

        // Determine Asset ID
        Guid assetId;
        if (TAsset.Locality is AssetLocality.Unique)
        {
            do
            {
                assetId = Guid.NewGuid();
            } while (assets.ContainsKey(assetId)); // just paranoid...
        }
        else
            assetId = TAsset.InfoToAssetId(info);

        // Check previous asset state
        if (assets.TryGetValue(assetId, out var assetState) && assetState.RefCount > 0)
        {
            SanityCheckSharedAsset(typeof(TAsset), assetState);
            assetState.RefCount++;
            if (assetState.Asset is null && (int)priority < (int)assetState.Priority)
                assetState.Priority = priority;
            return (assetId, assetState);
        }

        // Create new asset state
        TInfo infoCopy = info;
        assetState = new()
        {
            NeedsMainThreadDisposal = TAsset.NeedsMainThreadDisposal,
            LoadLazy = new(TaskCreationOptions.RunContinuationsAsynchronously),
            Factory = () => LoadAsset<TInfo, TAsset>(infoCopy, assetId),
            Tag = unchecked(++nextAssetTag),
            AssetType = typeof(TAsset),
            Priority = priority
        };
        assets[assetId] = assetState;
        localStats.OnAssetCreated();
        return (assetId, assetState);
    }

    [Conditional("DEBUG")]
    private static void SanityCheckSharedAsset(Type expectedType, AssetState asset)
    {
        if (asset.Asset is null) return;
        var actualType = asset.Asset.GetType();
        Debug.Assert(actualType.IsAssignableTo(expectedType), "Asset type mismatch, is this a GUID conflict?");
    }

    private async Task TryStartLoad(Guid assetId)
    {
        AssetState? assetState = null;
        using (await LockSemaphoreAsync(Cancellation))
        {
            assetState = assets.GetValueOrDefault(assetId);
            if (assetState is null or { RefCount: <= 0 })
            {
                // if the High load cannot start because all handles are disposed
                // then no one will know we never tried to load it in the first place
                return;
            }
        }

        await assetState.Factory();
    }

    private async Task<IDisposable> LoadAsset<TInfo, TAsset>(TInfo info, Guid assetId)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IAsset<TInfo>
    {
        // Due to AsyncLazy we can flow exceptions outside this method

        // Load asset
        var asset = (await TAsset.LoadAsync(this, assetId, info, Cancellation)).Asset;
        Debug.Assert(asset.Registry == this);
        CheckRegistryDisposal();

        // Propagate assets into registry state
        AssetState? assetState = null;
        using (await LockSemaphoreAsync(Cancellation))
        {
            assetState = assets.GetValueOrDefault(assetId);
            if (assetState is null or { RefCount: <= 0 })
                assetState = null;
            else
                localStats.OnAssetLoaded();
        }

        if (assetState is null)
        {
            // handle was disposed during load, now dispose the asset itself
            DisposeAssetObject(TAsset.NeedsMainThreadDisposal, asset);
            ObjectDisposedException.ThrowIf(true, typeof(TAsset));
        }

        CheckRegistryDisposal();
        return asset;

        [ExcludeFromCodeCoverage] // we cannot reasonably test that, it would be a race condition
        void CheckRegistryDisposal()
        {
            if (WasDisposed)
            {
                if (!TAsset.NeedsMainThreadDisposal)
                    asset.Dispose();
                // otherwise we are in a predicament and my decision is to leak a couple assets
                ObjectDisposedException.ThrowIf(true, typeof(AssetRegistry));
            }
            Cancellation.ThrowIfCancellationRequested();
        }
    }

    public bool TryGet<TAsset>(Guid assetId, out AssetHandle<TAsset> handle)
        where TAsset : class, IAsset
    {
        ObjectDisposedException.ThrowIf(WasDisposed, typeof(AssetRegistry));
        if (ParentRegistry?.TryGet(assetId, out handle) is true)
            return true;

        handle = default;
        using var releaser = LockSemaphore();
        if (!assets.TryGetValue(assetId, out var assetState) ||
            assetState.RefCount < 1 ||
            !assetState.AssetType.IsAssignableTo(typeof(TAsset)))
            return false;

        assetState.RefCount++;
        handle = new(this, assetId);
        return true;
    }

    public void Update() =>
        Update(MaxLowPriorityAssetsPerFrame);

    public void Update(int maxLowPrioAssets)
    {
        ObjectDisposedException.ThrowIf(WasDisposed, typeof(IAssetRegistry));
        if (!IsMainThread)
            throw new InvalidOperationException("Low batch scheduling is only allowed on the main thread");

        using (LockSemaphore())
        {
            DisposeOldAssets();

            for (int i = 0; i < maxLowPrioAssets && assetsToStart.Reader.TryRead(out var assetId); i++)
            {
                if (assets.TryGetValue(assetId, out var assetState) &&
                    assetState.LoadLazy != DisposedSource &&
                    !assetState.LoadLazy.Task.IsCompleted &&
                    !assetState.WasStarted)
                {
                    Task.Run(() => TryStartLoad(assetId), Cancellation);
                    assetState.WasStarted = true;
                }
            }

            // Copy apply actions and make sure assets keep alive during applying
            (applyActions, applyActionsBackup) = (applyActionsBackup, applyActions);
            for (int i = 0; i < applyActionsBackup.Count; i++)
            {
                var assetId = applyActionsBackup[i].assetId;
                if (!TryAddRefUnsafe(assetId))
                    // Asset is not alive anymore
                    applyActionsBackup[i] = default;
                else if (assets[assetId].Tag != applyActionsBackup[i].tag)
                {
                    // Asset was revived, apply actions are outdated
                    DelRefUnsafe(assetId);
                    applyActionsBackup[i] = default;
                }
                else if (assets[assetId].Asset is null)
                {
                    // Asset was not yet loaded
                    DelRefUnsafe(assetId);
                    applyActions.Add(applyActionsBackup[i]);
                    applyActionsBackup[i] = default;
                }
            }
        }

        // We can safely access applyActionsBackup as we are on the main thread
        var exceptions = new List<Exception>();
        foreach (var (assetId, _, assetType, action) in applyActionsBackup)
        {
            if (assetId == default)
                continue;
            try
            {
                applyActionCaster[assetType](assetId, action);
            }
            catch (Exception e)
            {
                exceptions.Add(e);
            }
        }

        // Remove reference we added earlier
        using (LockSemaphore())
        {
            foreach (var (assetId, _, _, _) in applyActionsBackup)
            {
                if (assetId != default)
                    DelRefUnsafe(assetId);
            }
        }
        applyActionsBackup.Clear();

        if (exceptions.Count > 0)
            throw new AggregateException(exceptions);
    }

    private void DisposeOldAssets()
    {
        Debug.Assert(IsMainThread);
        //Debug.Assert(semaphore.CurrentCount == 0);
        while (assetsToDispose.Reader.TryRead(out var asset))
        {
            asset.Dispose();
            localStats.OnAssetRemoved();
        }
    }

    public void Apply<TAsset>(AssetHandle<TAsset> handle, Action<AssetHandle<TAsset>> action)
        where TAsset : class, IAsset
    {
        ObjectDisposedException.ThrowIf(WasDisposed, typeof(IAssetRegistry));
        if (handle.Registry is null)
            throw new ArgumentException("Invalid asset handle");
        if (handle.Registry == ParentRegistry)
        {
            ParentRegistry.Apply(handle, action);
            return;
        }
        if (handle.Registry != this)
            throw new ArgumentException("Asset is not part of this registry or its parent");

        bool shouldBeExecutedNow = false;

        using (LockSemaphore())
        {
            if (!applyActionCaster.ContainsKey(typeof(TAsset)))
            {
                applyActionCaster.Add(typeof(TAsset), (assetId, action) =>
                {
                    ((Action<AssetHandle<TAsset>>)action)(new(this, assetId));
                });
            }

            if (IsMainThread)
            {
                ObjectDisposedException.ThrowIf(!TryAddRefUnsafe(handle.AssetId), typeof(IAsset));
                if (assets[handle.AssetId].Asset is null)
                    DelRefUnsafe(handle.AssetId); // Asset was not yet loaded
                else
                    shouldBeExecutedNow = true;
            }
            if (!shouldBeExecutedNow)
                applyActions.Add(new(handle.AssetId, assets[handle.AssetId].Tag, typeof(TAsset), action));
        }

        // Fast-path: no queueing 
        if (shouldBeExecutedNow)
        {
            try
            {
                action(new(this, handle.AssetId));
            }
            finally
            {
                (this as IAssetRegistryInternal).DelRef(handle.AssetId);
            }
        }
    }

    public void CopyDebugInfo(List<IAssetRegistry.AssetInfo> infos)
    {
        using (LockSemaphore())
        {
            infos.Clear();
            infos.EnsureCapacity(assets.Count);
            foreach (var (assetId, state) in assets)
            {
                var name =
                    state.Name ??
                    (state.Name = state.Asset?.ToString()) ??
                    $"Loading {state.AssetType.Name}";
                infos.Add(new(
                    assetId,
                    state.AssetType,
                    name,
                    state.RefCount,
                    state.Asset is not null,
                    state.Priority
                ));
            }
        }
    }
}
