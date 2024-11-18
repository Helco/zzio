using System;
using System.Diagnostics;

namespace zzre;

/// <summary>An untyped handle to an asset</summary>
/// <remarks>Keeping a handle to the asset keeps the asset alive</remarks>
public struct AssetHandle : IDisposable, IEquatable<AssetHandle>
{
    /// <summary>An invalid handle that is not tied to any registry nor asset</summary>
    /// <remarks>Only disposing this handle is an allowed action</remarks>
    public static readonly AssetHandle Invalid = new(registry: null!, Guid.Empty) { wasDisposed = true };

    internal readonly IAssetRegistryInternal registryInternal;
    private readonly AssetHandleScope? handleScope;
    private bool wasDisposed;

    /// <summary>The <see cref="IAssetRegistry"/> the asset was loaded at</summary>
    public readonly IAssetRegistry Registry => registryInternal;
    /// <summary>The unique ID of the asset</summary>
    public readonly Guid AssetID { get; }
    /// <summary>Checks whether this asset is marked as <see cref="AssetState.Loaded"/></summary>
    public readonly bool IsLoaded
    {
        get
        {
            CheckDefault();
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

    internal AssetHandle(AssetHandle original)
    {
        original.CheckDisposed();
        registryInternal = original.registryInternal;
        handleScope = original.handleScope;
        AssetID = original.AssetID;
        registryInternal.AddRefOf(original.AssetID);
    }

    /// <summary>Disposes the stake on the asset this handle is tied to</summary>
    /// <remarks>*May* trigger disposal of the asset and related secondary assets</remarks>
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
    internal readonly void CheckDisposed() =>
        ObjectDisposedException.ThrowIf(wasDisposed || AssetID == Guid.Empty, this);

    [Conditional("DEBUG")]
    private readonly void CheckDefault() =>
        ObjectDisposedException.ThrowIf(AssetID == Guid.Empty, this);

    /// <summary>Returns a loaded asset instance</summary>
    /// <remarks>The asset has to be marked as <see cref="AssetState.Loaded"/>, otherwise it will try to synchronously wait for loading completion</remarks>
    /// <typeparam name="TValue">The actual type of the asset instance</typeparam>
    /// <returns>The asset instance</returns>
    public readonly TValue Get<TValue>() where TValue : Asset
    {
        CheckDisposed();
        return registryInternal.GetLoadedAsset<TValue>(AssetID);
    }

    /// <summary>Returns this handle as a typed asset handle</summary>
    /// <remarks>This method does not check the actual type of the asset and will always succeed (given the handle was not disposed)</remarks>
    /// <typeparam name="TValue">The asset type to be used</typeparam>
    /// <returns>The typed asset handle</returns>
    public readonly AssetHandle<TValue> As<TValue>() where TValue : Asset
    {
        CheckDisposed();
        return (AssetHandle<TValue>)this;
    }

    /// <summary>Adds an apply action to the asset</summary>
    /// <remarks>Depending on whether the asset is already loaded the action will be called immediately or only stored for later execution</remarks>
    /// <typeparam name="TApplyContext">The type of the apply context given to the apply action</typeparam>
    /// <param name="applyFnptr">The function pointer to call as apply action</param>
    /// <param name="applyContext">The apply context given to the apply action</param>
    public readonly unsafe void Apply<TApplyContext>(
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
    {
        CheckDisposed();
        registryInternal.AddApplyAction(this, applyFnptr, in applyContext);
    }

    /// <summary>Adds an apply action to the asset</summary>
    /// <remarks>Depending on whether the asset is already loaded the action will be called immediately or only stored for later execution</remarks>
    /// <param name="applyAction">The delegate to call as apply action</param>
    public readonly void Apply(Action<AssetHandle> applyAction)
    {
        CheckDisposed();
        registryInternal.AddApplyAction(this, applyAction);
    }

    public override readonly string ToString() => $"AssetHandle {AssetID}";

    public override readonly bool Equals(object? obj) => obj is AssetHandle handle && Equals(handle);
    public readonly bool Equals(AssetHandle other) => AssetID.Equals(other.AssetID) && ReferenceEquals(registryInternal, other.registryInternal);
    public override readonly int GetHashCode() => HashCode.Combine(AssetID, registryInternal);
    public static bool operator ==(AssetHandle left, AssetHandle right) => left.Equals(right);
    public static bool operator !=(AssetHandle left, AssetHandle right) => !(left == right);
}

/// <summary>A typed asset handle for convenience</summary>
/// <remarks>The actual type is only checked upon retrieval of the instance</remarks>
/// <typeparam name="TValue">The type of the asset instance</typeparam>
public readonly struct AssetHandle<TValue> : IDisposable, IEquatable<AssetHandle<TValue>>, IEquatable<AssetHandle>
    where TValue : Asset
{
    /// <inheritdoc cref="AssetHandle.Invalid"/>
    public static readonly AssetHandle<TValue> Invalid = new() { Inner = AssetHandle.Invalid };

    /// <summary>The untyped <see cref="AssetHandle"/></summary>
    public AssetHandle Inner { get; private init; }

    public static explicit operator AssetHandle<TValue>(AssetHandle handle) => new() { Inner = handle };
    public static implicit operator AssetHandle(AssetHandle<TValue> handle) => handle.Inner;

    /// <inheritdoc cref="AssetHandle.Dispose"/>
    public void Dispose() => Inner.Dispose();
    /// <inheritdoc cref="AssetHandle.Get"/>
    public readonly TValue Get() => Inner.Get<TValue>();

    public override readonly string ToString() => $"AssetHandle<{typeof(TValue).Name}> {Inner.AssetID}";

    public static bool operator ==(AssetHandle<TValue> left, AssetHandle<TValue> right) => left.Equals(right);
    public static bool operator !=(AssetHandle<TValue> left, AssetHandle<TValue> right) => !(left == right);
    public override readonly bool Equals(object? obj) => obj is AssetHandle<TValue> handle && Equals(handle);
    public readonly bool Equals(AssetHandle<TValue> other) => Inner.AssetID.Equals(other.Inner.AssetID);
    public readonly bool Equals(AssetHandle other) => Inner.AssetID.Equals(other.AssetID);
    public override readonly int GetHashCode() => HashCode.Combine(Inner);
}
