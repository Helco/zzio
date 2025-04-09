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
        try
        {
            if (!Task.WhenAll(assets.Values
                     .Where(a => a.State is AssetState.Loading or AssetState.LoadingSecondary)
                     .Select(a => a.Complete(AssetLoadPriority.Synchronous).AsTask()))
                     .Wait(1000))
                throw new OperationCanceledException("Waiting for loading assets timed out.");
        }
        catch (Exception e) when (e is AggregateException or OperationCanceledException)
        {
            logger.Warning("Waiting for loading assets timed out. This is ignored and assets are disposed regardless.");
            logger.Debug(e, "Waiting for loading assets timed out.");
        }
        if (!Monitor.TryEnter(assets, 1000))
            logger.Warning("Could not lock assets in AssetRegistry disposal. This is ignored and assets are disposed regardless.");
        try
        {
            assetsToRemove.Writer.Complete();
            assetsToApply.Writer.Complete();
            assetsToStart.Writer.Complete();

            foreach (var asset in assets.Values)
                asset.Dispose();
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
            if (!assets.TryGetValue(guid, out var asset) || asset.State is AssetState.Disposed)
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

    /// <inheritdoc/>
    public unsafe AssetHandle Load<TInfo, TApplyContext>(
        in TInfo info,
        AssetLoadPriority priority,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
        where TInfo : IEquatable<TInfo>
    {
        var asset = GetOrCreateAsset(in info);

        asset.AddRef();
        LoadStart(asset, priority);
        LoadApply(asset, new FnPtrApplyAdapter<TApplyContext>(applyFnptr, applyContext));
        return new(this, asset.ID);
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
        if (addRef)
            asset.AddRef();
        LoadStart(asset, priority);
        LoadApply(asset, new ActionApplyAdapter(applyAction));
        return new(this, asset.ID);
    }

    private void LoadStart(IAsset asset, AssetLoadPriority priority)
    {
        if (asset.LoadTask.IsCompleted)
            return;

        switch(priority)
        {
            case AssetLoadPriority.Low: assetsToStart.Writer.TryWrite(asset); break;
            case AssetLoadPriority.High: _ = asset.LoadTask.Completion; break;
            case AssetLoadPriority.Synchronous:
                if (!IsMainThread)
                    throw new ArgumentException("Cannot use synchronous asset loading in secondary threads", nameof(priority));
                asset.LoadTask.Completion.AsTask().WaitAndRethrow(cancellationSource.Token);
                // Our cancellation token should be redundant, LoadTask should be cancelled by both the registry and the asset
                break;
            default: throw new NotImplementedException($"Unimplemented asset load priority: {priority}");
        }
    }

    private interface IApplyAdapter<T> where T : struct, IApplyAdapter<T>
    {
        bool IsNull { get; }
        Action<AssetHandle> AsAction { get; }
        void Invoke(AssetHandle handle);
    }

    private readonly struct ActionApplyAdapter(Action<AssetHandle>? action) : IApplyAdapter<ActionApplyAdapter>
    {
        public bool IsNull => action is null;
        public Action<AssetHandle> AsAction => action!;
        public void Invoke(AssetHandle handle) => action!.Invoke(handle);
    }

    private unsafe readonly struct FnPtrApplyAdapter<TApplyContext>(
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        TApplyContext applyContext
    ) : IApplyAdapter<FnPtrApplyAdapter<TApplyContext>>
    {
        public bool IsNull => applyFnptr is null;
        public Action<AssetHandle> AsAction
        {
            get
            {
                var fnptrCopy = applyFnptr;
                var contextCopy = applyContext;
                return handle => fnptrCopy(handle, in contextCopy);
            }
        }
        public void Invoke(AssetHandle handle) => applyFnptr(handle, in applyContext);
    }

    private void LoadApply<TApplyAdapter>(IAsset asset, in TApplyAdapter adapter)
        where TApplyAdapter : struct, IApplyAdapter<TApplyAdapter>
    {
        if (adapter.IsNull)
            return;
        var handle = new AssetHandle(this, asset.ID);
        if (asset.LoadTask.IsCompleted)
        {
            if (asset.LoadTask.Status is FFTaskStatus.Success)
            {
                if (IsMainThread)
                {
                    asset.ExecuteApplyActions(handle);
                    adapter.Invoke(handle);
                }
                else
                {
                    asset.AddApplyAction(adapter.AsAction);
                    assetsToApply.Writer.TryWrite(asset);
                }
            }
        }
        else
        {
            asset.AddApplyAction(adapter.AsAction);
        }
    }

    

    private static unsafe Action<AssetHandle>? ConvertFnptr<TContext>(
        delegate* managed<AssetHandle, ref readonly TContext, void> fnptr,
        in TContext context)
    {
        if (fnptr == null)
            return null;
        var contextCopy = context;
        return handle => fnptr(handle, in contextCopy);
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
        if (asset.State is AssetState.Loaded)
        {
            asset.AddRef();
            asset.ExecuteApplyActions(new(this, asset.ID));
            asset.DelRef();
        }
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
            asset.StartLoading(AssetLoadPriority.Low);
    }

    [Conditional("DEBUG")]
    private void EnsureMainThread([CallerMemberName] string methodName = "<null>")
    {
        if (!IsMainThread)
            throw new InvalidOperationException($"Cannot call AssetRegistry.{methodName} from secondary threads");
    }
}
