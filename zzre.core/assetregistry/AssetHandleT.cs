using System;
using System.Threading;
using System.Threading.Tasks;
using DotNext;
using DotNext.Threading.Tasks;

namespace zzre;

public interface IAssetHandle : IDisposable
{
    IAssetRegistry Registry { get; }
    Guid AssetId { get; }
}

public struct AssetHandle<TAsset>(IAssetRegistry registry, Guid assetId) : IAssetHandle, IEquatable<AssetHandle<TAsset>>
    where TAsset : class, IAsset
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

    public readonly TAsset? Asset
    {
        get
        {
            ThrowIfDisposed();
            var result = ((IAssetRegistryInternal)Registry).GetAsset(AssetId).ObserverTask.TryGetResult();
            return result?.IsSuccessful is true
                ? (TAsset)result.Value.Value
                : null;
        }
    }

    public readonly TAsset Get()
    {
        ThrowIfDisposed();
        if (!Registry.IsMainThread)
            throw new InvalidOperationException("Synchronous asset loading is only allowed on the main thread");
        var lazy = ((IAssetRegistryInternal)Registry).GetAsset(AssetId);
        if (!lazy.ObserverTask.IsCompleted)
        {
            try
            {
                lazy.WaitTask.Wait(Registry.Cancellation);
            }
            catch (AggregateException e)
            {
                throw e.InnerException ?? e;
            }
        }
        if (lazy.ObserverTask.Exception is AggregateException ex)
            throw (ex.InnerException ?? ex); // unpack the actual exception
        else
            return (TAsset)lazy.ObserverTask.Result;
    }

    public readonly ValueTask<TAsset> GetAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        var lazy = ((IAssetRegistryInternal)Registry).GetAsset(AssetId);
        if (lazy.ObserverTask.TryGetResult() is not Result<IDisposable> result)
            return new(DoGetAsync(ct));
        else if (result.IsSuccessful)
            return ValueTask.FromResult((TAsset)result.Value);
        else
            return ValueTask.FromException<TAsset>(result.Error!);
    }

    private readonly async Task<TAsset> DoGetAsync(CancellationToken ct)
    {
        var lazy = ((IAssetRegistryInternal)Registry).GetAsset(AssetId);
        return (TAsset)await lazy.WaitTask.WaitAsync(ct);
    }

    public AssetHandle As()
    {
        AssetHandle result = new(registry, assetId, wasDisposed);
        wasDisposed = true;
        return result;
    }

    public AssetHandle<TAsset> Move()
    {
        var result = this;
        wasDisposed = true;
        return result;
    }

    public readonly AssetHandle<TAsset> Duplicate()
    {
        ThrowIfDisposed();
        ((IAssetRegistryInternal)Registry).AddRef(AssetId);
        return new(Registry, AssetId, false);
    }

    public readonly AssetHandle AsDuplicate()
    {
        ThrowIfDisposed();
        ((IAssetRegistryInternal)Registry).AddRef(AssetId);
        return new(Registry, AssetId, false);
    }

    private readonly void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(wasDisposed, typeof(AssetHandle<TAsset>));
        ObjectDisposedException.ThrowIf(AssetId == Guid.Empty, typeof(AssetHandle<TAsset>));
        ObjectDisposedException.ThrowIf(Registry?.WasDisposed is null or true, typeof(IAssetRegistry));
    }

    public readonly override int GetHashCode() =>
        HashCode.Combine(Registry, AssetId);
    public readonly override bool Equals(object? obj) =>
        obj is AssetHandle<TAsset> other && Equals(other);
    public readonly bool Equals(AssetHandle<TAsset> other) =>
        Registry == other.Registry && AssetId == other.AssetId;
    public static bool operator ==(AssetHandle<TAsset> a, AssetHandle<TAsset> b) =>
        a.Equals(b);
    public static bool operator !=(AssetHandle<TAsset> a, AssetHandle<TAsset> b) =>
        !a.Equals(b);
}
