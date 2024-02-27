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
    private readonly IAssetRegistry apparentRegistry;
    private readonly Dictionary<Guid, IAsset> assets = [];
    private readonly CancellationTokenSource cancellationSource = new();
    private readonly Channel<IAsset> assetsToRemove = Channel.CreateUnbounded<IAsset>(ChannelOptions);
    private readonly Channel<IAsset> assetsToApply = Channel.CreateUnbounded<IAsset>(ChannelOptions);
    private readonly Channel<IAsset> assetsToStart = Channel.CreateUnbounded<IAsset>(ChannelOptions);
    private AssetRegistryStats stats;

    private CancellationToken Cancellation => cancellationSource.Token;
    private bool IsMainThread => mainThreadId == Environment.CurrentManagedThreadId;
    public ITagContainer DIContainer { get; }
    public AssetRegistryStats Stats => stats;

    public AssetRegistry(string debugName, ITagContainer diContainer) : this(debugName, diContainer, null) { }

    internal AssetRegistry(string debugName, ITagContainer diContainer, IAssetRegistry? apparentRegistry)
    {
        DIContainer = diContainer;
        this.apparentRegistry = apparentRegistry ?? this;
        if (string.IsNullOrEmpty(debugName))
            logger = diContainer.GetLoggerFor<AssetRegistry>();
        else
            logger = diContainer.GetTag<ILogger>().For($"{nameof(AssetRegistry)}-{debugName}");
    }

    protected override void DisposeManaged()
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
        if (AssetInfoRegistry<TInfo>.IsLocal && apparentRegistry == this)
            throw new InvalidOperationException("Cannot retrieve or create local assets in a global asset registry");

        var guid = AssetInfoRegistry<TInfo>.ToGuid(info);
        lock(assets)
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

    [Conditional("DEBUG")]
    private void EnsureMainThread([CallerMemberName] string methodName = "<null>")
    {
        if (!IsMainThread)
            throw new InvalidOperationException($"Cannot call AssetRegistry.{methodName} from secondary threads");
    }
}
