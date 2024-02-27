using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Serilog;

namespace zzre;

public sealed partial class AssetRegistry : IAssetRegistryInternal
{
    private static readonly int MaxLowPriorityAssetsPerFrame = Math.Max(1, Environment.ProcessorCount);
    private static readonly UnboundedChannelOptions ChannelOptions = new()
    {
        AllowSynchronousContinuations = true,
        SingleReader = true,
        SingleWriter = false
    };

    private readonly ILogger logger;
    private readonly int mainThreadId = Environment.CurrentManagedThreadId;
    private readonly Dictionary<Guid, IAsset> assets = [];
    private readonly CancellationTokenSource cancellationSource = new();
    private readonly Channel<IAsset> assetsToRemove = Channel.CreateUnbounded<IAsset>(ChannelOptions);
    private readonly Channel<IAsset> assetsToApply = Channel.CreateUnbounded<IAsset>(ChannelOptions);
    private readonly Channel<IAsset> assetsToStart = Channel.CreateUnbounded<IAsset>(ChannelOptions);
    private AssetRegistryStats stats;

    private bool IsMainThread => mainThreadId == Environment.CurrentManagedThreadId;
    internal bool IsLocalRegistry => ApparentRegistry != this;
    internal IAssetRegistry ApparentRegistry;
    public ITagContainer DIContainer { get; }
    public AssetRegistryStats Stats => stats;

    public AssetRegistry(string debugName, ITagContainer diContainer) : this(debugName, diContainer, null) { }

    internal AssetRegistry(string debugName, ITagContainer diContainer, IAssetRegistry? apparentRegistry)
    {
        DIContainer = diContainer;
        ApparentRegistry = apparentRegistry ?? this;
        if (string.IsNullOrEmpty(debugName))
            logger = diContainer.GetLoggerFor<AssetRegistry>();
        else
            logger = diContainer.GetTag<ILogger>().For($"{nameof(AssetRegistry)}-{debugName}");
    }

    public void Dispose()
    {
        EnsureMainThread();
        cancellationSource.Cancel();
        Task.WhenAll(assets.Values
            .Where(a => a.State is AssetState.Loading or AssetState.LoadingSecondary)
            .Select(a => a.LoadTask))
            .Wait(10000);
        assets.Clear();
        assetsToRemove.Writer.Complete();
        assetsToApply.Writer.Complete();
        assetsToStart.Writer.Complete();
        cancellationSource.Dispose();
        logger.Verbose("Finished disposing registry");
    }

    private IAsset GetOrCreateAsset<TInfo>(in TInfo info)
        where TInfo :  IEquatable<TInfo>
    {
        Cancellation.ThrowIfCancellationRequested();
        if (AssetInfoRegistry<TInfo>.IsLocal && !IsLocalRegistry)
            throw new InvalidOperationException("Cannot retrieve or create local assets in a global asset registry");

        var guid = AssetInfoRegistry<TInfo>.ToGuid(info);
        lock(assets)
        {
            if (!assets.TryGetValue(guid, out var asset) || asset.State is AssetState.Disposed)
            {
                logger.Verbose("New {Type} asset {Info} ({ID})", AssetInfoRegistry<TInfo>.Name, info, guid);
                stats.OnAssetCreated();
                asset = AssetInfoRegistry<TInfo>.Construct(ApparentRegistry, guid, in info);
                assets[guid] = asset;
                return asset;
            }
            asset.ThrowIfError();
            return asset;
        }
    }

    public unsafe AssetHandle Load<TInfo, TApplyContext>(
        in TInfo info,
        AssetLoadPriority priority,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
        where TInfo : IEquatable<TInfo>
    {
        var asset = GetOrCreateAsset(in info);
        lock (asset)
        {
            if (asset is { State: AssetState.Loaded } && IsMainThread)
            {
                // fast path: asset is already loaded and we only need to apply it
                asset.AddRef();
                var handle = new AssetHandle(this, asset.ID);
                applyFnptr(handle, in applyContext);
                return handle;
            }
            return LoadInner(asset, priority, ConvertFnptr(applyFnptr, applyContext));
        }
    }

    private static unsafe Action<AssetHandle> ConvertFnptr<TContext>(
        delegate* managed<AssetHandle, ref readonly TContext, void> fnptr,
        in TContext context)
    {
        var contextCopy = context;
        return handle => fnptr(handle, in contextCopy);
    }

    public AssetHandle Load<TInfo>(
        in TInfo info,
        AssetLoadPriority priority,
        Action<AssetHandle>? applyAction)
        where TInfo : IEquatable<TInfo>
    {
        var asset = GetOrCreateAsset(in info);
        lock (asset)
        {
            if (asset is { State: AssetState.Loaded } && IsMainThread)
            {
                asset.AddRef();
                var handle = new AssetHandle(this, asset.ID);
                applyAction?.Invoke(handle);
                return handle;
            }
            return LoadInner(asset, priority, applyAction);
        }
    }

    private AssetHandle LoadInner(IAsset asset, AssetLoadPriority priority, Action<AssetHandle>? applyAction)
    {
        // We assume that asset is locked for our thread during this method
        asset.AddRef();
        var handle = new AssetHandle(this, asset.ID);
        switch(asset.State)
        {
            case AssetState.Disposed or AssetState.Error:
                throw new ArgumentException("LoadInner was called with asset in unexpected state");

            case AssetState.Queued or AssetState.Loading or AssetState.LoadingSecondary:
                if (applyAction is not null)
                    asset.ApplyAction.Next += applyAction;
                if (asset.State == AssetState.Queued)
                    StartLoading(asset, priority);
                return handle;

            case AssetState.Loaded:
                if (IsMainThread)
                    applyAction?.Invoke(handle);
                else if (applyAction is not null)
                    assetsToApply.Writer.WriteAsync(asset, Cancellation).AsTask().WaitAndRethrow();
                return handle;

            default: throw new NotImplementedException($"Unimplemented asset state {asset.State}");
        }
    }

    private void StartLoading(IAsset asset, AssetLoadPriority priority)
    {
        // We assume that asset is locked for our thread during this method
        asset.Priority = priority;
        switch (priority)
        {
            case AssetLoadPriority.Synchronous:
                if (!asset.ApplyAction.IsEmpty && !IsMainThread)
                    throw new InvalidOperationException("Cannot load assets with Apply functions synchronously");
                asset.Complete();
                asset.ApplyAction.Invoke(new(this, asset.ID));
                break;
            case AssetLoadPriority.High:
                asset.StartLoading();
                break;
            case AssetLoadPriority.Low:
                assetsToStart.Writer.WriteAsync(asset, Cancellation).AsTask().WaitAndRethrow();
                break;
            default: throw new NotImplementedException($"Unimplemented asset load priority {priority}");
        }
    }

    private void RemoveAsset(IAsset asset)
    {
        if (asset.State is not (AssetState.Disposed or AssetState.Error))
            throw new InvalidOperationException($"Unexpected asset state for removal: {asset.State}");
        lock (assets)
            assets.Remove(asset.ID);
        logger.Verbose("Remove asset {Type} {ID}", asset.GetType().Name, asset.ID);
    }

    private void ApplyAsset(IAsset asset)
    {
        if (!asset.LoadTask.IsCompleted)
            throw new InvalidOperationException("Cannot apply assets that are not (internally) loaded");
        asset.ApplyAction.Invoke(new(this, asset.ID));
    }

    public void ApplyAssets()
    {
        EnsureMainThread();
        while (assetsToRemove.Reader.TryRead(out var asset))
            RemoveAsset(asset);
        while (assetsToApply.Reader.TryRead(out var asset))
            ApplyAsset(asset);

        for (int i = 0; i < MaxLowPriorityAssetsPerFrame && assetsToStart.Reader.TryRead(out var asset); i++)
        {
            lock (asset)
            {
                if (asset.State == AssetState.Queued)
                    asset.StartLoading();
            }
        }
    }

    private CancellationToken Cancellation => cancellationSource.Token;
    CancellationToken IAssetRegistryInternal.Cancellation => cancellationSource.Token;
    bool IAssetRegistryInternal.IsLocalRegistry => IsLocalRegistry;

    void IAssetRegistryInternal.DisposeHandle(AssetHandle handle)
    {
        if (handle.Registry != this)
            throw new ArgumentException("Tried to unload asset at wrong registry");
        lock (assets)
        {
            if (assets.TryGetValue(handle.AssetID, out var asset))
                asset.DelRef();
        }
    }

    private IAsset? TryGetForApplying(AssetHandle handle)
    {
        lock(assets)
        {
            var asset = assets.GetValueOrDefault(handle.AssetID);
            if (asset is not null)
            {
                asset.ThrowIfError();
                if (asset.State == AssetState.Disposed)
                    asset = null;
            }
            return asset;
        }
    }

    unsafe void IAssetRegistryInternal.AddApplyAction<TApplyContext>(AssetHandle handle,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
    {
        var asset = TryGetForApplying(handle);
        if (asset is null)
            return;
        lock(asset)
        {
            if (asset.State == AssetState.Loaded && IsMainThread)
                applyFnptr(handle, in applyContext);
            else
            {
                asset.ApplyAction.Next += ConvertFnptr(applyFnptr, in applyContext);
                if (asset.State == AssetState.Loaded)
                    assetsToApply.Writer.WriteAsync(asset, Cancellation).AsTask().Wait();
            }
        }
    }

    void IAssetRegistryInternal.AddApplyAction<TApplyContext>(AssetHandle handle,
        IAssetRegistry.ApplyWithContextAction<TApplyContext> applyAction,
        in TApplyContext applyContext)
    {
        var asset = TryGetForApplying(handle);
        if (asset is null)
            return;
        lock (asset)
        {
            if (asset.State == AssetState.Loaded && IsMainThread)
                applyAction(handle, in applyContext);
            else
            {
                var applyContextCopy = applyContext;
                asset.ApplyAction.Next += handle => applyAction(handle, in applyContextCopy);
                if (asset.State == AssetState.Loaded)
                    assetsToApply.Writer.WriteAsync(asset, Cancellation).AsTask().Wait();
            }
        }
    }

    void IAssetRegistryInternal.AddApplyAction(AssetHandle handle,
        Action<AssetHandle> applyAction)
    {
        var asset = TryGetForApplying(handle);
        if (asset is null)
            return;
        lock (asset)
        {
            if (asset.State == AssetState.Loaded && IsMainThread)
                applyAction(handle);
            else
            {
                asset.ApplyAction.Next += applyAction;
                if (asset.State == AssetState.Loaded)
                    assetsToApply.Writer.WriteAsync(asset, Cancellation).AsTask().Wait();
            }
        }
    }

    ValueTask IAssetRegistryInternal.QueueRemoveAsset(IAsset asset)
    {
        stats.OnAssetRemoved();
        if (IsMainThread)
        {
            RemoveAsset(asset);
            return ValueTask.CompletedTask;
        }
        else
            return assetsToRemove.Writer.WriteAsync(asset, Cancellation);
    }

    ValueTask IAssetRegistryInternal.QueueApplyAsset(IAsset asset)
    {
        stats.OnAssetLoaded();
        if (IsMainThread)
        {
            ApplyAsset(asset);
            return ValueTask.CompletedTask;
        }
        else
            return assetsToApply.Writer.WriteAsync(asset, Cancellation);
    }

    Task IAssetRegistryInternal.WaitAsyncAll(AssetHandle[] secondaryHandles)
    {
        lock (assets)
        {
            foreach (var handle in secondaryHandles)
            {
                if (assets.TryGetValue(handle.AssetID, out var asset))
                    asset.StartLoading();
            }
            return Task.WhenAll(secondaryHandles.Select(h => assets[h.AssetID].LoadTask));
        }
    }

    bool IAssetRegistryInternal.IsLoaded(Guid assetId)
    {
        EnsureMainThread();
        IAsset? asset;
        lock (assets)
            asset = assets.GetValueOrDefault(assetId);
        if (asset == null)
            return false;
        lock(asset)
            return asset.State == AssetState.Loaded;
    }

    TAsset IAssetRegistryInternal.GetLoadedAsset<TAsset>(Guid assetId)
    {
        EnsureMainThread();
        IAsset? asset;
        lock (assets)
            asset = assets.GetValueOrDefault(assetId);
        if (asset == null)
            throw new InvalidOperationException("Asset is not present in registry");
        if (asset is not TAsset)
            throw new InvalidOperationException($"Asset is not of type {typeof(TAsset).Name}");
        lock(asset)
        {
            asset.ThrowIfError();
            switch(asset.State)
            {
                case AssetState.Disposed:
                    throw new ObjectDisposedException(asset.ToString());
                default:
                    asset.Complete();
                    asset.ApplyAction.Invoke(new(this, assetId));
                    return (TAsset)asset;
            }
        }
    }

    [Conditional("DEBUG")]
    private void EnsureMainThread([CallerMemberName] string methodName = "<null>")
    {
        if (!IsMainThread)
            throw new InvalidOperationException($"Cannot call AssetRegistry.{methodName} from secondary threads");
    }
}
