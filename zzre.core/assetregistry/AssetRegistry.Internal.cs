using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace zzre;

public partial class AssetRegistry
{
    CancellationToken IAssetRegistryInternal.Cancellation => cancellationSource.Token;
    bool IAssetRegistryInternal.IsLocalRegistry => apparentRegistry != this;

    void IAssetRegistryInternal.DisposeHandle(AssetHandle handle)
    {
        if (handle.Registry != this)
            throw new ArgumentException("Tried to unload asset at wrong registry");
        if (WasDisposed)
            return; // not particularly nice, but all assets should be disposed anyway so ignoring handle disposal should be fine
        IAsset? asset = null;
        lock (assets)
            asset = assets.GetValueOrDefault(handle.AssetID);
        if (asset is null)
            throw new InvalidOperationException("Asset was not found or already deleted, this should not happen if you used a valid handle");
        asset.StateLock.Wait();
        try
        {
            asset.DelRef();
        }
        finally
        {
            asset.StateLock.Release();
        }
    }

    [Flags]
    private enum AddApplyActions
    {
        Execute = 1 << 0,
        Add = 1 << 1,
        Queue = 1 << 2
    }

    private IAsset? TryGetForApplying(AssetHandle handle)
    {
        lock (assets)
            return assets.GetValueOrDefault(handle.AssetID);
    }

    private AddApplyActions DecideForApplying(IAsset? asset)
    {
        if (asset is null || asset.State is AssetState.Error or AssetState.Disposed)
            return default;

        AddApplyActions actions = default;
        if (asset.State is AssetState.Loaded && IsMainThread)
            actions = AddApplyActions.Execute;
        else
        {
            actions |= AddApplyActions.Add;
            if (asset.State is AssetState.Loaded)
                actions |= AddApplyActions.Queue;
        }
        return actions;
    }

    unsafe void IAssetRegistryInternal.AddApplyAction<TApplyContext>(AssetHandle handle,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
    {
        var asset = TryGetForApplying(handle);
        if (asset is null)
            return;

        AddApplyActions actions;
        Action<AssetHandle>? previousApplyActions = null;
        asset.StateLock.Wait();
        try
        {
            actions = DecideForApplying(asset);
            if (actions.HasFlag(AddApplyActions.Execute))
                previousApplyActions = asset.ApplyAction.Reset();
            if (actions.HasFlag(AddApplyActions.Add))
                asset.ApplyAction.Next += ConvertFnptr(applyFnptr, in applyContext);
        }
        finally
        {
            asset.StateLock.Release();
        }

        if (actions.HasFlag(AddApplyActions.Queue))
            assetsToApply.Writer.TryWrite(asset);
        if (actions.HasFlag(AddApplyActions.Execute))
        {
            previousApplyActions?.Invoke(handle);
            applyFnptr(handle, in applyContext);
        }
    }

    void IAssetRegistryInternal.AddApplyAction(AssetHandle handle,
        Action<AssetHandle> applyAction)
    {
        var asset = TryGetForApplying(handle);
        if (asset is null)
            return;

        AddApplyActions actions;
        Action<AssetHandle>? previousApplyActions = null;
        asset.StateLock.Wait();
        try
        {
            actions = DecideForApplying(asset);
            if (actions.HasFlag(AddApplyActions.Execute))
                previousApplyActions = asset.ApplyAction.Reset();
            if (actions.HasFlag(AddApplyActions.Add) && applyAction is not null)
                asset.ApplyAction.Next += applyAction;
        }
        finally
        {
            asset.StateLock.Release();
        }

        if (actions.HasFlag(AddApplyActions.Queue))
            assetsToApply.Writer.TryWrite(asset);
        if (actions.HasFlag(AddApplyActions.Execute))
        {
            previousApplyActions?.Invoke(handle);
            applyAction?.Invoke(handle);
        }
    }

    void IAssetRegistryInternal.QueueRemoveAsset(IAsset asset)
    {
        stats.OnAssetRemoved();
        if (IsMainThread)
            RemoveAsset(asset);
        else
            assetsToRemove.Writer.TryWrite(asset);
    }

    void IAssetRegistryInternal.QueueApplyAsset(IAsset asset)
    {
        stats.OnAssetLoaded();
        if (IsMainThread)
            ApplyAsset(asset);
        else
            assetsToApply.Writer.TryWrite(asset);
    }

    Task IAssetRegistry.WaitAsyncAll(AssetHandle[] secondaryHandles)
    {
        // Task.WhenAll(IEnumerable) would create a List anyway. So we can use it
        // to prefilter, which we have to do anyways
        var secondaryTasks = new List<Task>(secondaryHandles.Length);
        lock (assets)
        {
            foreach (var handle in secondaryHandles)
            {
                handle.CheckDisposed();
                if (!assets.TryGetValue(handle.AssetID, out var asset))
                    continue;
                asset.StateLock.Wait();
                try
                {
                    asset.ThrowIfError();
                    if (asset.State == AssetState.Loaded)
                        continue;
                    else if (asset.State == AssetState.Queued)
                        asset.StartLoading();
                    secondaryTasks.Add(asset.LoadTask);
                }
                catch(Exception e) // if the asset was already erroneous
                {
                    throw new AggregateException([e]);
                }
                finally
                {
                    asset.StateLock.Release();
                }
            }
            return Task.WhenAll(secondaryTasks).WithAggregateException();
        }
    }

    bool IAssetRegistryInternal.IsLoaded(Guid assetId)
    {
        EnsureMainThread();
        IAsset? asset;
        lock (assets)
            asset = assets.GetValueOrDefault(assetId);
        return asset is IAsset { State: AssetState.Loaded };
        // no use locking the asset. If there is non-synchronized access it could just as well change before 
        // the caller decides on their action based on our return value.
    }

    TAsset IAssetRegistryInternal.GetLoadedAsset<TAsset>(Guid assetId)
    {
        IAsset? asset;
        lock (assets)
            asset = assets.GetValueOrDefault(assetId);
        if (asset == null)
            throw new InvalidOperationException("Asset is not present in registry");
        if (asset is not TAsset tasset)
            throw new InvalidOperationException($"Asset is not of type {typeof(TAsset).Name}");

        asset.StateLock.Wait();
        try
        {
            asset.ThrowIfError(); // so not disposed or error after this
            if (asset.State is AssetState.Loaded)
                return tasset;
        }
        finally
        {
            asset.StateLock.Release();
        }

        // We ended up having to (potentially start and) wait for loading.
        // Let's use the normal synchronous load mechanism for that
        Load(asset, AssetLoadPriority.Synchronous, applyAction: null, addRef: false);
        Debug.Assert(asset.State == AssetState.Loaded);
        return tasset;
    }

    ValueTask<TAsset> IAssetRegistryInternal.GetLoadedAssetAsync<TAsset>(Guid assetId)
    {
        IAsset? asset;
        lock (assets)
            asset = assets.GetValueOrDefault(assetId);
        if (asset == null)
            throw new InvalidOperationException("Asset is not present in registry");
        if (asset is not TAsset tasset)
            throw new InvalidOperationException($"Asset is not of type {typeof(TAsset).Name}");

        asset.StateLock.Wait();
        try
        {
            asset.ThrowIfError(); // so not disposed or error after this
            if (asset.State is AssetState.Loaded)
                return ValueTask.FromResult(tasset);
        }
        finally
        {
            asset.StateLock.Release();
        }

        // We still use the normal loading mechanism but asynchronously and wait ourselves
        Load(asset, AssetLoadPriority.High, applyAction: null, addRef: false);
        return new ValueTask<TAsset>(asset.LoadTask.ContinueWith(_ =>
        {
            Debug.Assert(asset.State == AssetState.Loaded); // on exception we should have thrown already
            return tasset;
        }));
    }

    void IAssetRegistryInternal.AddRefOf(Guid assetId)
    {
        IAsset? asset = null;
        lock (assets)
            asset = assets.GetValueOrDefault(assetId);
        if (asset is null)
            throw new InvalidOperationException("Asset was not found or already deleted, this should not happen if you used a valid handle");
        asset.StateLock.Wait();
        try
        {
            if (asset.State is AssetState.Disposed)
                throw new InvalidOperationException("Asset was already disposed, this should not happen if you used a valid handle");
            asset.AddRef();
        }
        finally
        {
            asset.StateLock.Release();
        }
    }
}
