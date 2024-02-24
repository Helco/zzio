using System;

namespace zzre;

public struct AssetHandle : IDisposable
{
    private readonly AssetHandleScope? handleScope;
    private readonly IAssetRegistry registry;
    private bool wasDisposed;

    public readonly Guid AssetID { get; }
    public readonly bool IsLoaded => registry.IsLoaded(AssetID);

    public AssetHandle(AssetHandleScope handleScope, Guid assetId)
    {
        this.handleScope = handleScope;
        registry = handleScope.Registry;
        AssetID = assetId;
    }

    public AssetHandle(AssetRegistry registry, Guid assetId)
    {
        this.registry = registry;
        AssetID = assetId;
    }

    public void Dispose()
    {
        if (wasDisposed)
            return;
        wasDisposed = true;
        if (handleScope is null)
            registry.Unload(this);
        else
            handleScope.Unload(this);
    }

    public readonly TValue Get<TValue>() where TValue : Asset => 
        registry.GetLoadedAsset<TValue>(AssetID);

    public readonly AssetHandle<TValue> As<TValue>() where TValue : Asset => this;

    public readonly override string ToString() => $"AssetHandle {AssetID}";
}

public struct AssetHandle<TValue> : IDisposable where TValue : Asset
{
    private AssetHandle inner;

    public static implicit operator AssetHandle<TValue>(AssetHandle handle) => new() { inner = handle };
    public static implicit operator AssetHandle(AssetHandle<TValue> handle) => handle.inner;
    public void Dispose() => inner.Dispose();
    public readonly TValue Get() => inner.Get<TValue>();

    public readonly override string ToString() => $"AssetHandle<{typeof(TValue).Name}> {inner.AssetID}";
}
