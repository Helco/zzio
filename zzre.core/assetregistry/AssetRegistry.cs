using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Threading;
using Serilog;
using Serilog.Core;

namespace zzre;

internal class AssetState
{
    public required AsyncLazy<IDisposable> LoadLazy;
    public IAssetHandle[] Secondaries = [];
    public IDisposable? Asset
    {
        get
        {
            var result = LoadLazy.Value;
            return result is null || !result.Value.IsSuccessful
                ? null : result.Value.Value;
        }
    }
    public int RefCount = 1;

    internal IAssetHandle[] Dispose()
    {
        RefCount = 0;
        Asset?.Dispose();
        var secondaries = Secondaries;
        Secondaries = [];
        LoadLazy = AssetRegistry.NullAssetLoadLazy;
        return secondaries;
    }
}

public class AssetRegistry : IAssetRegistryInternal
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(3);
    internal static readonly AsyncLazy<IDisposable> NullAssetLoadLazy = new(NullDisposable.Instance);

    private readonly Dictionary<Guid, AssetState> assets = [];
    private readonly CancellationTokenSource cancellationSource = new();
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly ILogger logger;
    private readonly int mainThreadId;
    private readonly AssetRegistry? parentRegistry;

    public bool WasDisposed => cancellationSource.IsCancellationRequested;
    public bool IsMainThread => mainThreadId == Environment.CurrentManagedThreadId;
    public bool IsLocalRegistry => ParentRegistry is not null;
    public IAssetRegistry? ParentRegistry => ParentRegistry;
    public CancellationToken Cancellation => cancellationSource.Token;

    public AssetRegistry(AssetRegistry? parent = null, ILogger? logger = null)
    {
        if (parent is { IsLocalRegistry: true })
            throw new ArgumentException("Cannot use a local registry as parent");
        parentRegistry = parent;
        this.logger = logger ?? Logger.None;
        mainThreadId = Environment.CurrentManagedThreadId;
    }

    public void Dispose()
    {
        if (WasDisposed)
            return;
        if (semaphore.Wait(LockTimeout, Cancellation))
            logger.Warning("AssetRegistry could not lock during dispose, going ahead nonetheless");
        cancellationSource.Cancel();
        foreach (var asset in assets.Values)
        {
            var secondaries = asset.Dispose();
            foreach (var secondary in secondaries)
            {
                if (secondary.Registry != this)
                    secondary.Dispose();
            }
        }
        assets.Clear();
        semaphore.Dispose();
        cancellationSource.Dispose();
    }

    public void StartNextLowBatch()
    {
        ObjectDisposedException.ThrowIf(WasDisposed, typeof(IAssetRegistry));
        throw new NotImplementedException();
    }

    void IAssetRegistryInternal.AddRef(Guid assetId)
    {
        ObjectDisposedException.ThrowIf(WasDisposed, typeof(IAssetRegistry));
        if (!semaphore.Wait(LockTimeout, Cancellation))
            throw new InvalidOperationException("Could not lock registry");
        try
        {
            var assetState = assets.GetValueOrDefault(assetId);
            ObjectDisposedException.ThrowIf(assetState is null || assetState.RefCount <= 0, typeof(AssetState));
            assetState.RefCount++;
        }
        finally
        {
            semaphore.Release();
        }
    }

    void IAssetRegistryInternal.DelRef(Guid assetId)
    {
        if (WasDisposed) return; // Ignore out-of-order deletion, all assets are already dead
        if (!semaphore.Wait(LockTimeout, Cancellation))
            throw new InvalidOperationException("Could not lock registry");
        try
        {
            var assetState = assets.GetValueOrDefault(assetId);
            if (assetState is null || assetState.RefCount <= 0)
                return; // Let's just ignore already-dead assets, we got what we wanted
            if (--assetState.RefCount <= 0)
            {
                assetState.Dispose();
                assets.Remove(assetId);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    AsyncLazy<IDisposable> IAssetRegistryInternal.GetAsset(Guid assetId) =>
        assets.GetValueOrDefault(assetId)?.LoadLazy ?? NullAssetLoadLazy;

    public Task WaitForAll(IEnumerable<IAssetHandle> assetHandles, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(WasDisposed, typeof(IAssetRegistry));
        if (assetHandles.Any(h => h.Registry != this && h.Registry != ParentRegistry))
            throw new ArgumentException("Cannot wait for assets from a foreign registry");
        return Task.WhenAll(assetHandles
                    .Select(id =>
                        assets.GetValueOrDefault(id.AssetId) ??
                        parentRegistry?.assets.GetValueOrDefault(id.AssetId))
                    .Where(state =>
                        state != null &&
                        state.LoadLazy != NullAssetLoadLazy)
                    .Select(state => state!.LoadLazy.WithCancellation(ct)));
    }

    public AssetHandle<TAsset> Load<TInfo, TAsset>(in TInfo info, AssetPriority priority)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IAsset<TInfo>
    {
        ObjectDisposedException.ThrowIf(WasDisposed, typeof(IAssetRegistry));
        if (TAsset.Locality is not AssetLocality.Global && !IsLocalRegistry)
            throw new ArgumentException($"Cannot load a local asset {typeof(TAsset).FullName} from a global registry");
        if (TAsset.Locality is AssetLocality.Global && IsLocalRegistry)
            return parentRegistry!.Load<TInfo, TAsset>(info, priority);

        var (assetId, assetState) = GetOrCreateAssetState<TInfo, TAsset>(info);
        var handle = new AssetHandle<TAsset>(this, assetId);
        if (!assetState.LoadLazy.IsValueCreated)
        {
            switch (priority)
            {
                case AssetPriority.Synchronous:
                    try
                    {
                        handle.Get(); // checks main thread
                    }
                    catch (Exception)
                    {
                        // the user does not get the handle, so there shouldn't be a refcount on the asset
                        (this as IAssetRegistryInternal).DelRef(assetId);
                        throw;
                    }
                    break;
                case AssetPriority.High:
                    Task.Run(() => handle.GetAsync(Cancellation), Cancellation);
                    break;
                case AssetPriority.Low:
                    throw new NotImplementedException();
            }            
        }
        return handle;
    }

    private (Guid, AssetState) GetOrCreateAssetState<TInfo, TAsset>(in TInfo info)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IAsset<TInfo>
    {
        var assetId = TAsset.InfoToAssetId(info);

        if (!semaphore.Wait(LockTimeout, Cancellation))
            throw new InvalidOperationException("Could not lock registry, what is happening?");
        try
        {
            if (assets.TryGetValue(assetId, out var assetState) && assetState.RefCount > 0)
            {
                assetState.RefCount++;
                return (assetId, assetState);
            }

            TInfo infoCopy = info;
            assetState = new()
            {
                LoadLazy = new(ct => LoadAsset<TInfo, TAsset>(infoCopy, assetId))
            };
            assets[assetId] = assetState;
            return (assetId, assetState);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<IDisposable> LoadAsset<TInfo, TAsset>(TInfo info, Guid assetId)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IAsset<TInfo>
    {
        // Due to AsyncLazy we can flow exceptions outside this method

        // Load asset and secondary assets
        var (asset, secondaries) = await TAsset.LoadAsync(info, Cancellation);
        if (secondaries.Any())
        {
            try
            {
                await WaitForAll(secondaries, Cancellation);
            }
            finally
            {
                asset.Dispose();
            }
        }

        // Propagate assets into registry state
        if (!await semaphore.WaitAsync(LockTimeout, Cancellation))
            throw new InvalidOperationException("Could not lock registry, what is happening?");
        try
        {
            var assetState = assets.GetValueOrDefault(assetId);
            ObjectDisposedException.ThrowIf(assetState is null or { RefCount: <= 0 }, typeof(AssetState));
            assetState.Secondaries = [.. secondaries];
        }
        catch (Exception)
        {
            asset.Dispose();
            foreach (var secondary in secondaries)
                secondary.Dispose();
            throw;
        }
        finally
        {
            semaphore.Release();
        }

        return asset;
    }
}
