using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNext;
using DotNext.Threading;
using Serilog;
using Serilog.Core;

namespace zzre;

internal class AssetState
{
    public required AsyncLazy<IDisposable> LoadLazy;
    public IAssetHandle[] Secondaries = [];
    public IDisposable? Asset;
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

    private readonly Dictionary<Type, IAssetLoader> loaders = [];
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
        loaders.Clear();
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
        where TAsset : class, IDisposable
    {
        ObjectDisposedException.ThrowIf(WasDisposed, typeof(IAssetRegistry));
        if ((parentRegistry is not null && !parentRegistry.loaders.TryGetValue(typeof(TInfo), out var loader)) ||
            !loaders.TryGetValue(typeof(TInfo), out loader))
            throw new ArgumentException($"No loader registered for info type: {typeof(TInfo).FullName}");
        if (loader.AssetType != typeof(TAsset))
            throw new ArgumentException($"Registered loader is for asset type {loader.AssetType.FullName} and not for {typeof(TAsset).FullName}");
        if (loader.Locality is not AssetLocality.Global && !IsLocalRegistry)
            throw new ArgumentException($"Cannot load a local asset {typeof(TAsset).FullName} from a global registry");
        if (loader.Locality is AssetLocality.Global && IsLocalRegistry)
            return parentRegistry!.Load<TInfo, TAsset>(info, priority);
        var typedLoader = loader as IAssetLoader<TInfo, TAsset>;
        Debug.Assert(typedLoader is not null);

        var (assetId, assetState) = GetOrCreateAssetState(typedLoader, info);
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

    private (Guid, AssetState) GetOrCreateAssetState<TInfo, TAsset>(IAssetLoader<TInfo, TAsset> typedLoader, in TInfo info)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IDisposable
    {
        var assetId = typedLoader!.InfoToAssetId(info);

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
                LoadLazy = new(ct => LoadAsset(typedLoader, infoCopy, assetId))
            };
            assets[assetId] = assetState;
            return (assetId, assetState);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public void RegisterLoader<TInfo, TAsset>(IAssetLoader<TInfo, TAsset> loader)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IDisposable
    {
        ObjectDisposedException.ThrowIf(WasDisposed, typeof(IAssetRegistry));
        if (ParentRegistry is not null)
        {
            ParentRegistry.RegisterLoader(loader);
            return;
        }
    }

    private Task<IDisposable> LoadAsset<TInfo, TAsset>(IAssetLoader<TInfo, TAsset> loader, in TInfo info, Guid assetId)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IDisposable
    {
        return Task.FromResult<IDisposable>(NullDisposable.Instance);
    }
}
