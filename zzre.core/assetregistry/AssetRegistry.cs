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

internal interface IAssetRegistryInternal : IAssetRegistry
{
    ValueTask QueueRemoveAsset(IAsset asset);
    ValueTask QueueApplyAsset(IAsset asset);
    Task WaitAsyncAll(AssetHandle[] assets);
    bool IsLoaded(Guid assetId);
    TAsset GetLoadedAsset<TAsset>(Guid assetId);
}

public sealed partial class AssetRegistry : IAssetRegistryInternal
{
    private static readonly int MaxLowPriorityAssetsPerFrame = Math.Max(1, Environment.ProcessorCount / 4);
    private static readonly BoundedChannelOptions ChannelOptions = new(128)
    {
        AllowSynchronousContinuations = true,
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    };

    private readonly ILogger logger;
    private readonly int mainThreadId = Environment.CurrentManagedThreadId;
    private readonly Dictionary<Guid, IAsset> assets = [];
    private readonly CancellationTokenSource cancellationSource = new();
    private readonly Channel<IAsset> assetsToRemove = Channel.CreateBounded<IAsset>(ChannelOptions);
    private readonly Channel<IAsset> assetsToApply = Channel.CreateBounded<IAsset>(ChannelOptions);
    private readonly Channel<IAsset> assetsToStart = Channel.CreateBounded<IAsset>(ChannelOptions);

    private bool IsMainThread => mainThreadId == Environment.CurrentManagedThreadId;
    internal CancellationToken Cancellation => cancellationSource.Token;
    internal bool IsLocalRegistry { get; }
    public ITagContainer DIContainer { get; }

    public AssetRegistry(string debugName, ITagContainer diContainer) : this(debugName, diContainer, isLocalRegistry: false) { }

    internal AssetRegistry(string debugName, ITagContainer diContainer, bool isLocalRegistry)
    {
        DIContainer = diContainer;
        if (string.IsNullOrEmpty(debugName))
            logger = diContainer.GetLoggerFor<AssetRegistry>();
        else
            logger = diContainer.GetTag<ILogger>().For($"{nameof(AssetRegistry)}-{debugName}");
        IsLocalRegistry = isLocalRegistry;
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
                asset = AssetInfoRegistry<TInfo>.Construct(DIContainer, guid, in info);
                assets[guid] = asset;
                return asset;
            }
            else if (asset.State is AssetState.Error)
            {
                asset.LoadTask.WaitAndRethrow();
                throw new InvalidOperationException("Asset was marked as erroneous but does not contain an exception");
            }
            else
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
        Action<AssetHandle>? applyAction = null)
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

    public void Unload(AssetHandle handle)
    {
        lock (assets)
        {
            if (assets.TryGetValue(handle.AssetID, out var asset))
                asset.DelRef();
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

    ValueTask IAssetRegistryInternal.QueueRemoveAsset(IAsset asset)
    {
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
            switch(asset.State)
            {
                case AssetState.Disposed:
                    throw new ObjectDisposedException(asset.ToString());
                case AssetState.Error:
                    asset.LoadTask.WaitAndRethrow();
                    throw new InvalidOperationException("Asset was marked erroneous but did not contain an exception");
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
