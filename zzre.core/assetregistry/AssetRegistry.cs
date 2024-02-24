using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace zzre;

internal interface IAssetRegistry : IAssetHandleScope
{
    ValueTask QueueRemoveAsset(IAsset asset);
    ValueTask QueueApplyAsset(IAsset asset);
    Task WaitAsyncAll(AssetHandle[] assets);
    bool IsLoaded(Guid assetId);
    TAsset GetLoadedAsset<TAsset>(Guid assetId);
}

public partial class AssetRegistry(ITagContainer diContainer) : IAssetRegistry
{
    private static readonly int MaxLowPriorityAssetsPerFrame = Math.Max(1, Environment.ProcessorCount / 4);
    private static readonly BoundedChannelOptions ChannelOptions = new(128)
    {
        AllowSynchronousContinuations = true,
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    };

    private readonly int mainThreadId = Environment.CurrentManagedThreadId;
    private readonly Dictionary<Type, Func<Guid, IAsset>> assetConstructors = [];
    private readonly Dictionary<Guid, IAsset> assets = [];
    private readonly CancellationTokenSource cancellationSource = new();
    private readonly Channel<IAsset> assetsToRemove = Channel.CreateBounded<IAsset>(ChannelOptions);
    private readonly Channel<IAsset> assetsToApply = Channel.CreateBounded<IAsset>(ChannelOptions);
    private readonly Channel<IAsset> assetsToStart = Channel.CreateBounded<IAsset>(ChannelOptions);

    private bool IsMainThread => mainThreadId == Environment.CurrentManagedThreadId;
    internal CancellationToken Cancellation => cancellationSource.Token;

    public void Dispose()
    {
        EnsureMainThread();
        cancellationSource.Cancel();
        Task.WhenAll(assets.Values
            .Where(a => a.State is AssetState.Loading or AssetState.LoadingSecondary)
            .Select(a => a.LoadTask))
            .Wait(10000);
        assets.Clear();
        assetConstructors.Clear();
        assetsToRemove.Writer.Complete();
        assetsToApply.Writer.Complete();
        assetsToStart.Writer.Complete();
        cancellationSource.Dispose();
    }

    public void RegisterAssetType<TInfo>(Func<ITagContainer, Guid, TInfo, Asset> constructor)
        where TInfo : IEquatable<TInfo>
    {
        EnsureMainThread();
        Cancellation.ThrowIfCancellationRequested();
        Func<Guid, IAsset> assetConstructor = id =>
            constructor(diContainer, id, AssetInfoRegistry<TInfo>.ToInfo(id));
        if (!assetConstructors.TryAdd(typeof(TInfo), assetConstructor))
            throw new ArgumentException("Asset type is already registered", nameof(TInfo));
    }

    public void RegisterAssetType<
        TInfo,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAsset>()
        where TInfo : IEquatable<TInfo>
        where TAsset : Asset
    {
        var ctorInfo =
            typeof(TAsset).GetConstructor([typeof(ITagContainer), typeof(Guid), typeof(TInfo)])
            ?? throw new ArgumentException("Could not find standard constructor", nameof(TAsset));
        RegisterAssetType<TInfo>((registry, guid, info) => (TAsset)ctorInfo.Invoke([registry, guid, info]));
    }

    private IAsset GetOrCreateAsset<TInfo>(in TInfo info)
        where TInfo :  IEquatable<TInfo>
    {
        Cancellation.ThrowIfCancellationRequested();
        var guid = AssetInfoRegistry<TInfo>.ToGuid(info);
        lock(assets)
        {
            if (!assets.TryGetValue(guid, out var asset) || asset.State is AssetState.Disposed)
            {
                asset = assetConstructors[typeof(TInfo)].Invoke(guid);
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
        var asset = GetOrCreateAsset(info);
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
        var asset = GetOrCreateAsset(info);
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

    ValueTask IAssetRegistry.QueueRemoveAsset(IAsset asset)
    {
        if (IsMainThread)
        {
            RemoveAsset(asset);
            return ValueTask.CompletedTask;
        }
        else
            return assetsToRemove.Writer.WriteAsync(asset, Cancellation);
    }

    ValueTask IAssetRegistry.QueueApplyAsset(IAsset asset)
    {
        if (IsMainThread)
        {
            ApplyAsset(asset);
            return ValueTask.CompletedTask;
        }
        else
            return assetsToApply.Writer.WriteAsync(asset, Cancellation);
    }

    Task IAssetRegistry.WaitAsyncAll(AssetHandle[] secondaryHandles)
    {
        lock (assets)
        {
            return Task.WhenAll(secondaryHandles.Select(h => assets[h.AssetID].LoadTask));
        }
    }

    bool IAssetRegistry.IsLoaded(Guid assetId)
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

    TAsset IAssetRegistry.GetLoadedAsset<TAsset>(Guid assetId)
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
