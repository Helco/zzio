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
            assetsToRemove.Writer.Complete();
            assetsToApply.Writer.Complete();
            assetsToStart.Writer.Complete();

            bool failedToDisposeSomeAsset = false;
            foreach (var asset in assets.Values)
            {
                if (asset.StateLock.Wait(500))
                {
                    asset.Dispose();
                    asset.StateLock.Release();
                }
                else if (!failedToDisposeSomeAsset)
                {
                    logger.Warning("Failed to dispose all assets, someone holds an asset state lock for way to long");
                    failedToDisposeSomeAsset = true;   
                }
            }
            assets.Clear();
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
            if (!assets.TryGetValue(guid, out var asset) || asset.State is AssetState.Disposed or AssetState.Error)
            {
                if (asset is null)
                    logger.Verbose("New {Type} asset {Info} ({ID})", AssetInfoRegistry<TInfo>.Name, info, guid);
                else
                    logger.Verbose("New {Type} asset {Info} ({ID}) (previous: {PreviousState})", AssetInfoRegistry<TInfo>.Name, info, guid, asset.State);
                stats.OnAssetCreated();
                asset = AssetInfoRegistry<TInfo>.Construct(apparentRegistry, guid, in info);
                assets[guid] = asset;
                return asset;
            }
            asset.ThrowIfError();
            return asset;
        }
    }

    private static bool IsFinalState(AssetState state) => 
        state is AssetState.Loaded or AssetState.Disposed or AssetState.Error;

    [Flags]
    private enum LoadActions
    {
        Complete = 1 << 0,
        Apply = 1 << 1,
        QueueApply = 1 << 2
    }

    private LoadActions DecideLoadActions(IAsset asset, AssetLoadPriority priority)
    {
        Debug.Assert(asset.StateLock.CurrentCount == 0);
        LoadActions actions = default;

        if (asset.State is AssetState.Queued)
        {
            if (priority is AssetLoadPriority.Low)
                assetsToStart.Writer.TryWrite(asset);
            else
                asset.StartLoading();
        }
        if (priority == AssetLoadPriority.Synchronous)
        {
            if (!IsMainThread)
                throw new InvalidOperationException("Cannot load synchronous assets on secondary threads.");
            actions |= LoadActions.Complete | LoadActions.Apply;
        }
        else
            actions |= IsMainThread && asset.State is AssetState.Loaded
                ? LoadActions.Apply
                : LoadActions.QueueApply;

        // If we neither Apply nor QueueApply we lose apply actions
        // If we both Apply and QueueApply we duplicate apply actions
        Debug.Assert(actions.HasFlag(LoadActions.Apply) ^ actions.HasFlag(LoadActions.QueueApply));
        return actions;
    }

    /// <inheritdoc/>
    public unsafe AssetHandle Load<TInfo, TApplyContext>(
        in TInfo info,
        AssetLoadPriority priority,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
        where TInfo : IEquatable<TInfo>
    {
        var asset = GetOrCreateAsset(in info);
        LoadActions loadActions;
        Action<AssetHandle>? previousApplyActions = null;

        asset.StateLock.Wait();
        try
        {
            asset.AddRef();
            loadActions = DecideLoadActions(asset, priority);
            if (loadActions.HasFlag(LoadActions.Apply))
                previousApplyActions = asset.ApplyAction.Reset();
            if (loadActions.HasFlag(LoadActions.QueueApply))
                asset.ApplyAction.Next += ConvertFnptr(applyFnptr, in applyContext);
        }
        finally
        {
            asset.StateLock.Release();
        }

        var handle = new AssetHandle(this, asset.ID);
        if (loadActions.HasFlag(LoadActions.Complete))
            asset.LoadTask.WaitAndRethrow();
        asset.ThrowIfError();
        if (loadActions.HasFlag(LoadActions.Apply))
        {
            previousApplyActions?.Invoke(handle);
            applyFnptr(handle, in applyContext);
        }
        return handle;
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
        => Load(GetOrCreateAsset(in info), priority, applyAction, addRef: true);

    private AssetHandle Load(
        IAsset asset,
        AssetLoadPriority priority,
        Action<AssetHandle>? applyAction,
        bool addRef)
    {
        LoadActions loadActions;
        Action<AssetHandle>? previousApplyActions = null;

        asset.StateLock.Wait();
        try
        {
            if (addRef)
                asset.AddRef();
            loadActions = DecideLoadActions(asset, priority);
            if (loadActions.HasFlag(LoadActions.Apply))
                previousApplyActions = asset.ApplyAction.Reset();
            if (loadActions.HasFlag(LoadActions.QueueApply) && applyAction is not null)
                asset.ApplyAction.Next += applyAction;
        }
        finally
        {
            asset.StateLock.Release();
        }

        var handle = new AssetHandle(this, asset.ID);
        if (loadActions.HasFlag(LoadActions.Complete))
            asset.LoadTask.WaitAndRethrow();
        asset.ThrowIfError();
        if (loadActions.HasFlag(LoadActions.Apply))
        {
            previousApplyActions?.Invoke(handle);
            applyAction?.Invoke(handle);
        }
        return handle;
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

    /// <inheritdoc/>
    public void ApplyAssets()
    {
        EnsureMainThread();
        while (assetsToRemove.Reader.TryRead(out var asset))
            RemoveAsset(asset);
        while (assetsToApply.Reader.TryRead(out var asset))
            ApplyAsset(asset);

        for (int i = 0; i < MaxLowPriorityAssetsPerFrame && assetsToStart.Reader.TryRead(out var asset); i++)
        {
            asset.StateLock.Wait();
            try
            {
                if (asset.State == AssetState.Queued)
                    asset.StartLoading();
            }
            finally
            {
                asset.StateLock.Release();
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
