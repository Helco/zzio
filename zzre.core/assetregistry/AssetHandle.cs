using System;
using System.Diagnostics;

namespace zzre;

public struct AssetHandle : IDisposable
{
    private readonly AssetHandleScope? handleScope;
    internal readonly IAssetRegistryInternal registryInternal;
    private bool wasDisposed;

    public readonly IAssetRegistry Registry => registryInternal;
    public readonly Guid AssetID { get; }
    public readonly bool IsLoaded
    {
        get
        {
            CheckDisposed();
            return registryInternal.IsLoaded(AssetID);
        }
    }

    public AssetHandle(IAssetRegistry registry, AssetHandleScope handleScope, Guid assetId)
    {
        this.handleScope = handleScope;
        this.registryInternal = registry as IAssetRegistryInternal ??
            throw new ArgumentException("Cannot create asset handles from registry decorators", nameof(registry));
        AssetID = assetId;
    }

    public AssetHandle(AssetHandleScope handleScope, Guid assetId)
    {
        this.handleScope = handleScope;
        registryInternal = (handleScope as IAssetRegistry).InternalRegistry;
        AssetID = assetId;
    }

    public AssetHandle(AssetRegistry registry, Guid assetId)
    {
        registryInternal = registry;
        AssetID = assetId;
    }

    public void Dispose()
    {
        if (wasDisposed)
            return;
        wasDisposed = true;
        if (handleScope is null)
            registryInternal.DisposeHandle(this);
        else
            handleScope.DisposeHandle(this);
    }

    [Conditional("DEBUG")]
    private readonly void CheckDisposed() =>
        ObjectDisposedException.ThrowIf(wasDisposed, this);

    public readonly TValue Get<TValue>() where TValue : Asset
    {
        CheckDisposed();
        return registryInternal.GetLoadedAsset<TValue>(AssetID);
    }

    public readonly AssetHandle<TValue> As<TValue>() where TValue : Asset
    {
        CheckDisposed();
        return (AssetHandle<TValue>)this;
    }

    public unsafe readonly void Apply<TApplyContext>(
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
    {
        CheckDisposed();
        registryInternal.AddApplyAction(this, applyFnptr, in applyContext);
    }

    public readonly void Apply<TApplyContext>(
        IAssetRegistry.ApplyWithContextAction<TApplyContext> applyAction,
        in TApplyContext applyContext)
    {
        CheckDisposed();
        registryInternal.AddApplyAction(this, applyAction, in applyContext);
    }

    public readonly void Apply(Action<AssetHandle> applyAction)
    {
        CheckDisposed();
        registryInternal.AddApplyAction(this, applyAction);
    }

    public readonly override string ToString() => $"AssetHandle {AssetID}";
}

public struct AssetHandle<TValue> : IDisposable where TValue : Asset
{
    public AssetHandle Inner { get; private init; }

    public static explicit operator AssetHandle<TValue>(AssetHandle handle) => new() { Inner = handle };
    public static implicit operator AssetHandle(AssetHandle<TValue> handle) => handle.Inner;
    public void Dispose() => Inner.Dispose();
    public readonly TValue Get() => Inner.Get<TValue>();

    public readonly override string ToString() => $"AssetHandle<{typeof(TValue).Name}> {Inner.AssetID}";
}
