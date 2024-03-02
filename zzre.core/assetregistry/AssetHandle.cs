using System;
using System.Diagnostics;

namespace zzre;

public struct AssetHandle : IDisposable, IEquatable<AssetHandle>
{
    public static readonly AssetHandle Invalid = new(registry: null!, Guid.Empty) { wasDisposed = true };

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

    internal AssetHandle(IAssetRegistry registry, AssetHandleScope handleScope, Guid assetId)
    {
        this.handleScope = handleScope;
        this.registryInternal = registry as IAssetRegistryInternal ??
            throw new ArgumentException("Cannot create asset handles from registry decorators", nameof(registry));
        AssetID = assetId;
    }

    internal AssetHandle(AssetHandleScope handleScope, Guid assetId)
    {
        this.handleScope = handleScope;
        registryInternal = (handleScope as IAssetRegistry).InternalRegistry;
        AssetID = assetId;
    }

    internal AssetHandle(AssetRegistry registry, Guid assetId)
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
            registryInternal?.DisposeHandle(this);
        else
            handleScope.DisposeHandle(this);
    }

    [Conditional("DEBUG")]
    private readonly void CheckDisposed() =>
        ObjectDisposedException.ThrowIf(wasDisposed || AssetID == Guid.Empty, this);

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

    public override readonly bool Equals(object? obj) => obj is AssetHandle handle && Equals(handle);
    public readonly bool Equals(AssetHandle other) => AssetID.Equals(other.AssetID);
    public override readonly int GetHashCode() => HashCode.Combine(AssetID);
    public static bool operator ==(AssetHandle left, AssetHandle right) => left.Equals(right);
    public static bool operator !=(AssetHandle left, AssetHandle right) => !(left == right);
}

public struct AssetHandle<TValue> : IDisposable, IEquatable<AssetHandle<TValue>>, IEquatable<AssetHandle>
    where TValue : Asset
{
    public static readonly AssetHandle<TValue> Invalid = new() { Inner = AssetHandle.Invalid };

    public AssetHandle Inner { get; private init; }

    public static explicit operator AssetHandle<TValue>(AssetHandle handle) => new() { Inner = handle };
    public static implicit operator AssetHandle(AssetHandle<TValue> handle) => handle.Inner;

    public void Dispose() => Inner.Dispose();
    public readonly TValue Get() => Inner.Get<TValue>();

    public readonly override string ToString() => $"AssetHandle<{typeof(TValue).Name}> {Inner.AssetID}";

    public static bool operator ==(AssetHandle<TValue> left, AssetHandle<TValue> right) => left.Equals(right);
    public static bool operator !=(AssetHandle<TValue> left, AssetHandle<TValue> right) => !(left == right);
    public override readonly bool Equals(object? obj) => obj is AssetHandle<TValue> handle && Equals(handle);
    public readonly bool Equals(AssetHandle<TValue> other) => Inner.AssetID.Equals(other.Inner.AssetID);
    public readonly bool Equals(AssetHandle other) => Inner.AssetID.Equals(other.AssetID);
    public override readonly int GetHashCode() => HashCode.Combine(Inner);
}
