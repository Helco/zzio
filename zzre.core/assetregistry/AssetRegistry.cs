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

/*
 * As global note to the AssetRegistry, as far as I see using ChannelWriter.TryWrite without
 * checking the result is correct, as we always use a SingleConsumerUnboundChannelWriter so
 * the only way TryWrite returns false is if the writer was completed in which not writing
 * the item is correct.
 */

/// <summary>A global asset registry to facilitate loading, retrieval and disposal of assets</summary>
public sealed partial class AssetRegistry : zzio.BaseDisposable, IAssetRegistryInternal
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
    /// <summary>The apparent registry is the interface given to assets, it will differ for local registries</summary>s
    private readonly IAssetRegistry apparentRegistry;
    private readonly Dictionary<Guid, IAsset> assets = [];
    private readonly CancellationTokenSource cancellationSource = new();
    private readonly Channel<IAsset> assetsToRemove = Channel.CreateUnbounded<IAsset>(ChannelOptions);
    private readonly Channel<IAsset> assetsToApply = Channel.CreateUnbounded<IAsset>(ChannelOptions);
    private readonly Channel<IAsset> assetsToStart = Channel.CreateUnbounded<IAsset>(ChannelOptions);
    private AssetRegistryStats stats;

    private CancellationToken Cancellation => cancellationSource.Token;
    private bool IsMainThread => mainThreadId == Environment.CurrentManagedThreadId;
    /// <inheritdoc/>
    public ITagContainer DIContainer { get; }
    /// <inheritdoc/>
    public AssetRegistryStats Stats => stats;

    /// <summary>Constructs a global asset registry</summary>
    /// <param name="debugName">A name for debugging purposes (used in logs)</param>
    /// <param name="diContainer">The <see cref="ITagContainer"/> given to this registry for loading the asset contents</param>
    public AssetRegistry(string debugName, ITagContainer diContainer) : this(debugName, diContainer, null) { }

    internal AssetRegistry(string debugName, ITagContainer diContainer, IAssetRegistry? apparentRegistry)
    {
        DIContainer = diContainer;
        this.apparentRegistry = apparentRegistry ?? this;
        logger = string.IsNullOrEmpty(debugName)
            ? diContainer.GetLoggerFor<AssetRegistry>()
            : diContainer.GetTag<ILogger>().For($"{nameof(AssetRegistry)}-{debugName}");
    }

    protected override void DisposeManaged()
    {
        if (!IsMainThread)
            logger.Warning("AssetRegistry.Dispose is called from a secondary thread.");
        cancellationSource.Cancel();
        Task.WhenAll(assets.Values
            .Where(a => a.State is AssetState.Loading or AssetState.LoadingSecondary)
            .Select(a => a.LoadTask))
            .Wait(10000);
        if (!Monitor.TryEnter(assets, 1000))
            logger.Warning("Could not lock assets in AssetRegistry disposal. This is ignored and assets are disposed regardless.");
        try
        {
            foreach (var asset in assets.Values)
                asset.Dispose();
            assets.Clear();
            assetsToRemove.Writer.Complete();
            assetsToApply.Writer.Complete();
            assetsToStart.Writer.Complete();
        }
        finally
        {
            if (Monitor.IsEntered(assets))
                Monitor.Exit(assets);
        }
        cancellationSource.Dispose();
        logger.Verbose("Finished disposing registry");
    }

    private IAsset GetOrCreateAsset<TInfo>(in TInfo info)
        where TInfo : IEquatable<TInfo>
    {
        Cancellation.ThrowIfCancellationRequested();
        if (AssetInfoRegistry<TInfo>.Locality is not AssetLocality.Global && apparentRegistry == this)
            throw new InvalidOperationException("Cannot retrieve or create local assets in a global asset registry");

        var guid = AssetInfoRegistry<TInfo>.ToGuid(info);
        lock (assets)
        {
            if (!assets.TryGetValue(guid, out var asset) || asset.State is AssetState.Disposed)
            {
                logger.Verbose("New {Type} asset {Info} ({ID})", AssetInfoRegistry<TInfo>.Name, info, guid);
                stats.OnAssetCreated();
                asset = AssetInfoRegistry<TInfo>.Construct(apparentRegistry, guid, in info);
                assets[guid] = asset;
                return asset;
            }
            asset.ThrowIfError();
            return asset;
        }
    }

    private AssetHandle TryFastPathLoad(IAsset asset)
    {
        // If we managed the fast path we have a valid handle and are on the main thread
        // No secondary thread should be able to delete that asset as long as we have the
        // handle (except for asynchronous registry disposal which is bad news anyway)
        if (IsMainThread && asset is { State: AssetState.Loaded })
        {
            using var _ = asset.StateLock.Lock();
            if (asset.State is AssetState.Loaded)
            {
                asset.AddRef();
                return new AssetHandle(this, asset.ID);
            }
        }
        return AssetHandle.Invalid;
    }

    /// <inheritdoc/>
    public unsafe AssetHandle Load<TInfo, TApplyContext>(
        in TInfo info,
        AssetLoadPriority priority,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
        where TInfo : IEquatable<TInfo>
    {
        if (priority is AssetLoadPriority.Synchronous && !IsMainThread)
            throw new InvalidOperationException("Cannot load synchronous from secondary threads");

        var asset = GetOrCreateAsset(in info);
        var fastHandle = TryFastPathLoad(asset);
        if (fastHandle == AssetHandle.Invalid)
            return LoadInner(asset, priority, ConvertFnptr(applyFnptr, applyContext));
        applyFnptr(fastHandle, in applyContext);
        return fastHandle;
        
    }

    private static unsafe Action<AssetHandle> ConvertFnptr<TContext>(
        delegate* managed<AssetHandle, ref readonly TContext, void> fnptr,
        in TContext context)
    {
        var contextCopy = context;
        return handle => fnptr(handle, in contextCopy);
    }

    /// <inheritdoc/>
    public AssetHandle Load<TInfo>(
        in TInfo info,
        AssetLoadPriority priority,
        Action<AssetHandle>? applyAction)
        where TInfo : IEquatable<TInfo>
    {
        if (priority is AssetLoadPriority.Synchronous && !IsMainThread)
            throw new InvalidOperationException("Cannot load synchronous from secondary threads");

        var asset = GetOrCreateAsset(in info);
        var fastHandle = TryFastPathLoad(asset);
        if (fastHandle == AssetHandle.Invalid)
            return LoadInner(asset, priority, applyAction);
        applyAction?.Invoke(fastHandle);
        return fastHandle;
    }

    private AssetHandle LoadInner(IAsset asset, AssetLoadPriority priority, Action<AssetHandle>? applyAction)
    {
        var assetHandle = new AssetHandle(this, asset.ID);
        AssetState decisionState;
        using (var _ = asset.StateLock.Lock())
        {
            asset.AddRef();
            decisionState = asset.State;

            // Queue apply action
            if (decisionState is AssetState.Queued or AssetState.Loading or AssetState.LoadingSecondary)
                asset.ApplyAction.Next += applyAction;
            else if (decisionState is AssetState.Loaded && !IsMainThread)
            {
                if (asset.ApplyAction.IsEmpty)
                    (this as IAssetRegistryInternal).QueueApplyAsset(asset);
                asset.ApplyAction.Next += applyAction;
            }
            
            if (decisionState is AssetState.Queued)
            {
                if (priority == AssetLoadPriority.Low)
                    assetsToStart.Writer.TryWrite(asset);
                else
                    asset.StartLoading();
            }
        }

        if (decisionState is AssetState.Disposed or AssetState.Error)
        {
            asset.ThrowIfError();
            throw new InvalidOperationException("Asset load decision state was erroneous but no exception was thrown. This is a grave inconsistency in AssetRegistry");
        }
        if (priority is AssetLoadPriority.Synchronous && decisionState is not AssetState.Loaded)
        {
            asset.Complete();
            applyAction?.Invoke(assetHandle);
        }
        
        if (IsMainThread && (decisionState is AssetState.Loaded || priority is AssetLoadPriority.Synchronous))
            applyAction?.Invoke(assetHandle);
        return assetHandle;
    }

    private void StartLoading(IAsset asset, AssetLoadPriority priority)
    {
        // We assume that asset is locked for our thread during this method
        asset.Priority = priority;
        switch (priority)
        {
            case AssetLoadPriority.Synchronous:
                if (!asset.ApplyAction.IsEmpty && !IsMainThread)
                    throw new InvalidOperationException("Cannot load assets with Apply functions synchronously on secondary threads");
                asset.Complete();
                asset.ApplyAction.Invoke(new(this, asset.ID));
                break;
            case AssetLoadPriority.High:
                asset.StartLoading();
                break;
            case AssetLoadPriority.Low:
                assetsToStart.Writer.TryWrite(asset);
                break;
            default: throw new NotImplementedException($"Unimplemented asset load priority {priority}");
        }
    }

    private void RemoveAsset(IAsset asset)
    {
        Debug.Assert(Monitor.IsEntered(assets));
        if (asset.State is not (AssetState.Disposed or AssetState.Error))
            throw new InvalidOperationException($"Unexpected asset state for removal: {asset.State}");
        assets.Remove(asset.ID);
        logger.Verbose("Remove asset {Type} {ID}", asset.GetType().Name, asset.ID);
    }

    private void ApplyAsset(IAsset asset)
    {
        Action<AssetHandle>? applyAction = null;
        using (var _ = asset.StateLock.Lock())
        {
            if (!asset.LoadTask.IsCompleted)
                throw new InvalidOperationException("Cannot apply assets that are not (internally) loaded");
            applyAction = asset.ApplyAction.MoveOut();
        }
        applyAction?.Invoke(new(this, asset.ID));
    }

    /// <inheritdoc/>
    public void ApplyAssets()
    {
        EnsureMainThread();
        lock(assets)
        {
            while (assetsToRemove.Reader.TryRead(out var asset))
                RemoveAsset(asset);
        }
        while (assetsToApply.Reader.TryRead(out var asset))
            ApplyAsset(asset);

        for (int i = 0; i < MaxLowPriorityAssetsPerFrame && assetsToStart.Reader.TryRead(out var asset); i++)
        {
            using var _ = asset.StateLock.Lock();
            if (asset.State == AssetState.Queued)
                asset.StartLoading();
        }
    }

    [Conditional("DEBUG")]
    private void EnsureMainThread([CallerMemberName] string methodName = "<null>")
    {
        if (!IsMainThread)
            throw new InvalidOperationException($"Cannot call AssetRegistry.{methodName} from secondary threads");
    }
}
