using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace zzre;

internal interface IAssetRegistry
{
    ValueTask QueueRemoveAsset(IAsset asset);
    ValueTask QueueApplyAsset(IAsset asset);
}

public enum AssetLoadPriority
{
    Synchronous,
    High,
    Low
}

public class AssetRegistry : IAssetRegistry
{
    private static readonly BoundedChannelOptions ChannelOptions = new(256)
    {
        AllowSynchronousContinuations = true,
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    };

    private readonly int mainThreadId = Environment.CurrentManagedThreadId;
    private readonly Dictionary<Guid, IAsset> assets = [];
    private readonly CancellationTokenSource cancellationSource = new();
    private readonly Channel<IAsset> assetsToRemove = Channel.CreateBounded<IAsset>(ChannelOptions);
    private readonly Channel<IAsset> assetsToApply = Channel.CreateBounded<IAsset>(ChannelOptions);

    private bool IsMainThread => mainThreadId == Environment.CurrentManagedThreadId;
    internal CancellationToken Cancellation => cancellationSource.Token;

    private IAsset? TryGetAsset<TInfo>(in TInfo info)
    {

    }

    public unsafe AssetHandle Load<TInfo, TApplyContext>(
        in TInfo info,
        AssetLoadPriority priority,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
    {
        var asset = TryGetAsset(info);
        if (asset == null)
            return Load(info, priority, ConvertFnptr(applyFnptr, applyContext));

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
        }

        return Load(info, priority, ConvertFnptr(applyFnptr, applyContext));
    }

    private unsafe Action<AssetHandle> ConvertFnptr<TContext>(
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
    {
        var asset = TryGetAsset(info);
        if (asset is { State: AssetState.Loaded } && IsMainThread)
        {
            asset.AddRef();
            var handle = new AssetHandle(this, asset.ID);
            applyAction?.Invoke(handle);
            return handle;
        }


    }

    public void Unload(AssetHandle handle, bool ignoreInvalidHandles = false)
    {
        var asset = TryGetAsset(info);
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

    }

    public void ApplyAssets()
    {
        EnsureMainThread();
        while (assetsToRemove.Reader.TryRead(out var asset))
            RemoveAsset(asset);
        while (assetsToApply.Reader.TryRead(out var asset))
            ApplyAsset(asset);
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

    [Conditional("DEBUG")]
    private void EnsureMainThread([CallerMemberName] string methodName = "<null>")
    {
        if (!IsMainThread)
            throw new InvalidOperationException($"Cannot call AssetRegistry.{methodName} from secondary threads");
    }
}
