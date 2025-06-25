using System;
using System.Threading;
using System.Threading.Tasks;
using DotNext;

namespace zzre;

/*public record struct AssetHandle(IAssetRegistry Registry, Guid AssetId) : IDisposable
{
    private bool wasDisposed;
    private IDisposable? _asset;
    private IDisposable? AssetOpt
    {
        get => Volatile.Read(ref _asset);
        set => Interlocked.Exchange(ref _asset, value);
    }

    internal AssetHandle(IAssetRegistry registry, Guid assetId, bool wasDisposed, IDisposable? asset) : this(registry, assetId)
    {
        this.wasDisposed = true;
        this.AssetOpt = asset;
    }

    public void Dispose()
    {
        if (wasDisposed) return;
        wasDisposed = true;
        AssetOpt = null;
        ((IAssetRegistryInternal)Registry).DelRef(AssetId);
    }

    public IDisposable? Asset
    {
        get
        {
            ObjectDisposedException.ThrowIf(wasDisposed, typeof(AssetHandle<IDisposable>));
            if (AssetOpt is null)
            {
                var result = ((IAssetRegistryInternal)Registry).GetAsset(AssetId).Value;
                if (result?.IsSuccessful is true)
                    AssetOpt = result.Value.Value;
            }
            return AssetOpt;
        }
    }

    public IDisposable Get()
    {
        Registry.ThrowIfNotMainThread();
        if (AssetOpt is not null)
            return AssetOpt;
        var lazy = ((IAssetRegistryInternal)Registry).GetAsset(AssetId);
        if (!lazy.IsValueCreated)
            lazy.WithCancellation(Registry.Cancellation).Wait(Registry.Cancellation);
        return AssetOpt = lazy.Value!.Value.Value; // throws on error
    }

    public ValueTask<IDisposable> GetAsync(CancellationToken ct)
    {
        if (AssetOpt is IDisposable prevAsset)
            return ValueTask.FromResult(prevAsset);
        return new(DoGetAsync(ct));
    }

    private async Task<IDisposable> DoGetAsync(CancellationToken ct)
    {
        var lazy = ((IAssetRegistryInternal)Registry).GetAsset(AssetId);
        return AssetOpt = await lazy.WithCancellation(ct);
    }

    public AssetHandle<TAsset> As<TAsset>() where TAsset : class, IDisposable
    {
        var result = new AssetHandle<TAsset>(Registry, AssetId, wasDisposed, (TAsset?)AssetOpt);
        wasDisposed = true;
        AssetOpt = null;
        return result;
    }

    public AssetHandle AsDuplicate<TAsset>() where TAsset : class, IDisposable
    {
        ObjectDisposedException.ThrowIf(wasDisposed, typeof(AssetHandle));
        ((IAssetRegistryInternal)Registry).AddRef(AssetId);
        return new(Registry, AssetId, false, AssetOpt);
    }

    public AssetHandle Duplicate()
    {
        ObjectDisposedException.ThrowIf(wasDisposed, typeof(AssetHandle));
        ((IAssetRegistryInternal)Registry).AddRef(AssetId);
        return new(Registry, AssetId, false, AssetOpt);
    }
}*/


public interface IAssetHandle : IDisposable
{
    IAssetRegistry Registry { get; }
    Guid AssetId { get; }
}

public struct AssetHandle<TAsset>(IAssetRegistry registry, Guid assetId) : IAssetHandle, IEquatable<AssetHandle<TAsset>>
    where TAsset : class, IDisposable
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
        ((IAssetRegistryInternal)Registry).DelRef(AssetId);
    }

    public readonly TAsset? Asset
    {
        get
        {
            ThrowIfDisposed();
            var result = ((IAssetRegistryInternal)Registry).GetAsset(AssetId).Value;
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
        if (!lazy.IsValueCreated)
        {
            try
            {
                lazy.WithCancellation(Registry.Cancellation).Wait(Registry.Cancellation);
            }
            catch (AggregateException e)
            {
                throw e.InnerException ?? e;
            }
        }
        return (TAsset)lazy.Value!.Value; // throws on error
    }

    public readonly ValueTask<TAsset> GetAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        var lazy = ((IAssetRegistryInternal)Registry).GetAsset(AssetId);
        if (lazy.Value is not Result<IDisposable> result)
            return new(DoGetAsync(ct));
        else if (result.IsSuccessful)
            return ValueTask.FromResult((TAsset)result.Value);
        else
            return ValueTask.FromException<TAsset>(result.Error);
    }

    private readonly async Task<TAsset> DoGetAsync(CancellationToken ct)
    {
        var lazy = ((IAssetRegistryInternal)Registry).GetAsset(AssetId);
        return (TAsset)await lazy.WithCancellation(ct);
    }

    /*public AssetHandle As()
    {
        var result = new AssetHandle(Registry, AssetId, wasDisposed, AssetOpt);
        wasDisposed = true;
        AssetOpt = null;
        return result;
    }

    public AssetHandle AsDuplicate()
    {
        ObjectDisposedException.ThrowIf(wasDisposed, typeof(AssetHandle<TAsset>));
        ((IAssetRegistryInternal)Registry).AddRef(AssetId);
        return new(Registry, AssetId, false, AssetOpt);
    }*/

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
