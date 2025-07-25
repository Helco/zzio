using System;
using System.Diagnostics;
namespace zzre;

public struct AssetHandle(IAssetRegistry registry, Guid assetId) : IAssetHandle, IEquatable<AssetHandle>
{
    private bool wasDisposed;
    public readonly IAssetRegistry Registry => registry;
    public readonly Guid AssetId => assetId;

    internal AssetHandle(IAssetRegistry registry, Guid assetId, bool wasDisposed) : this(registry, assetId)
    {
        this.wasDisposed = wasDisposed;
    }

    public void Dispose()
    {
        if (wasDisposed) return;
        wasDisposed = true;
        if (Registry is not null && AssetId != default)
            ((IAssetRegistryInternal)Registry).DelRef(AssetId);
    }

    public AssetHandle<TAsset> As<TAsset>()
        where TAsset : class, IAsset
    {
        TypeCheck(typeof(TAsset));
        AssetHandle<TAsset> result = new(registry, assetId, wasDisposed);
        wasDisposed = true;
        return result;
    }

    public readonly AssetHandle<TAsset> AsDuplicate<TAsset>()
        where TAsset : class, IAsset
    {
        ThrowIfDisposed();
        TypeCheck(typeof(TAsset));
        ((IAssetRegistryInternal)Registry).AddRef(AssetId);
        return new(Registry, AssetId, false);
    }

    public AssetHandle Move()
    {
        var result = this;
        wasDisposed = true;
        return result;
    }

    public readonly AssetHandle Duplicate()
    {
        ThrowIfDisposed();
        ((IAssetRegistryInternal)Registry).AddRef(AssetId);
        return new(Registry, AssetId, false);
    }

    private readonly void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(wasDisposed, typeof(AssetHandle));
        ObjectDisposedException.ThrowIf(AssetId == Guid.Empty, typeof(AssetHandle));
        ObjectDisposedException.ThrowIf(Registry?.WasDisposed is null or true, typeof(IAssetRegistry));
    }

    [Conditional("DEBUG")]
    private readonly void TypeCheck(Type type)
    {
        if (wasDisposed || Registry?.WasDisposed is null or true || AssetId == Guid.Empty)
            return;
        ((IAssetRegistryInternal)Registry).CheckType(AssetId, type);
    }

    public readonly override int GetHashCode() =>
        HashCode.Combine(Registry, AssetId);
    public readonly override bool Equals(object? obj) =>
        obj is AssetHandle other && Equals(other);
    public readonly bool Equals(AssetHandle other) =>
        Registry == other.Registry && AssetId == other.AssetId;
    public static bool operator ==(AssetHandle a, AssetHandle b) =>
        a.Equals(b);
    public static bool operator !=(AssetHandle a, AssetHandle b) =>
        !a.Equals(b);
}
