using System;

namespace zzre;

public struct AssetHandle : IDisposable
{
    private readonly AssetHandleScope? handleScope;
    internal readonly IAssetRegistryInternal registryInternal;
    private bool wasDisposed;

    public readonly IAssetRegistry Registry => registryInternal;
    public readonly Guid AssetID { get; }
    public readonly bool IsLoaded => registryInternal.IsLoaded(AssetID);

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
            registryInternal.Unload(this);
        else
            handleScope.Unload(this);
    }

    public readonly TValue Get<TValue>() where TValue : Asset => 
        registryInternal.GetLoadedAsset<TValue>(AssetID);

    public readonly AssetHandle<TValue> As<TValue>() where TValue : Asset => (AssetHandle<TValue>)this;

    public readonly override string ToString() => $"AssetHandle {AssetID}";
}

public struct AssetHandle<TValue> : IDisposable where TValue : Asset
{
    private AssetHandle inner;

    public static explicit operator AssetHandle<TValue>(AssetHandle handle) => new() { inner = handle };
    public static implicit operator AssetHandle(AssetHandle<TValue> handle) => handle.inner;
    public void Dispose() => inner.Dispose();
    public readonly TValue Get() => inner.Get<TValue>();

    public readonly override string ToString() => $"AssetHandle<{typeof(TValue).Name}> {inner.AssetID}";
}
