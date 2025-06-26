using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DotNext.Threading;
using Serilog;
using Serilog.Core;

namespace zzre;

internal sealed class AssetState
{
    public required bool NeedsMainThreadDisposal;
    public required AsyncLazy<IDisposable> LoadLazy;
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
}

public class AssetRegistry : IAssetRegistryInternal
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(3);
    internal static readonly AsyncLazy<IDisposable> NullAssetLoadLazy = new(NullDisposable.Instance);
    private static readonly int MaxLowPriorityAssetsPerFrame = Math.Clamp(Environment.ProcessorCount, 1, 64);
    private static readonly UnboundedChannelOptions ChannelOptions = new()
    {
        AllowSynchronousContinuations = true,
        SingleReader = true,
        SingleWriter = false
    };

    private readonly Channel<Guid> assetsToStart = Channel.CreateUnbounded<Guid>(ChannelOptions);
    private readonly Channel<IDisposable> assetsToDispose = Channel.CreateUnbounded<IDisposable>(ChannelOptions);
    private readonly Dictionary<Guid, AssetState> assets = [];
    private readonly CancellationTokenSource cancellationSource = new();
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly ILogger logger;
    private readonly int mainThreadId;
    private readonly AssetRegistry? parentRegistry;

    public bool WasDisposed => cancellationSource.IsCancellationRequested;
    public bool IsMainThread => mainThreadId == Environment.CurrentManagedThreadId;
    public bool IsLocalRegistry => ParentRegistry is not null;
    public IAssetRegistry? ParentRegistry => parentRegistry;
    public CancellationToken Cancellation => cancellationSource.Token;
    public ITagContainer DIContainer { get; }

    public AssetRegistry(ITagContainer diContainer, AssetRegistry? parent = null, string? debugName = null)
    {
        DIContainer = diContainer;
        ObjectDisposedException.ThrowIf(parent is { WasDisposed: true }, typeof(AssetRegistry));
        if (parent is { IsLocalRegistry: true })
            throw new ArgumentException("Cannot use a local registry as parent");
        parentRegistry = parent;
        logger = // ILogger is optional, as well as the log prefix
            !diContainer.TryGetTag<ILogger>(out var parentLogger) ? Logger.None
            : string.IsNullOrEmpty(debugName) ? diContainer.GetLoggerFor<AssetRegistry>()
            : diContainer.GetTag<ILogger>().For($"{nameof(AssetRegistry)}-{debugName}");
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
            DisposeAssetState(asset);
        }
        assets.Clear();
        DisposeOldAssets(); // after current assets in case we add something into it (we shouldn't)

        semaphore.Dispose();
        cancellationSource.Dispose();
        logger.Verbose("Finished disposing registry");
    }

    private void DisposeAssetState(AssetState state)
    {
        if (IsMainThread || !state.NeedsMainThreadDisposal)
            state.Asset?.Dispose();
        else if (state.Asset is not null)
        {
            var success = assetsToDispose.Writer.TryWrite(state.Asset);
            Debug.Assert(success);
        }

        state.RefCount = 0;
        state.LoadLazy = NullAssetLoadLazy;
    }

    [ExcludeFromCodeCoverage] // we cannot reasonably check for semaphore failure
    private void LockSemaphore()
    {
        if (!semaphore.Wait(LockTimeout, Cancellation))
            throw new InvalidOperationException("Could not lock asset registry");
        // this should only happen in bug scenarios
    }

    [ExcludeFromCodeCoverage]
    private async Task LockSemaphoreAsync(CancellationToken ct)
    {
        if (!await semaphore.WaitAsync(LockTimeout, Cancellation))
            throw new InvalidOperationException("Could not lock asset registry");
    }

    void IAssetRegistryInternal.AddRef(Guid assetId)
    {
        ObjectDisposedException.ThrowIf(WasDisposed, typeof(IAssetRegistry));
        LockSemaphore();
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

    [ExcludeFromCodeCoverage]
    void IAssetRegistryInternal.DelRef(Guid assetId)
    {
        if (WasDisposed) return; // Ignore out-of-order deletion, all assets are already dead
        LockSemaphore();
        try
        {
            var assetState = assets.GetValueOrDefault(assetId);
            if (assetState is null || assetState.RefCount <= 0)
                return; // Let's just ignore already-dead assets, we got what we wanted
            if (--assetState.RefCount <= 0)
            {
                DisposeAssetState(assetState);
                assets.Remove(assetId);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    AsyncLazy<IDisposable> IAssetRegistryInternal.GetAsset(Guid assetId)
    {
        AssetState asset;
        LockSemaphore();
        try
        {
            ObjectDisposedException.ThrowIf(!assets.TryGetValue(assetId, out asset!), nameof(IAssetHandle));
        }
        finally
        {
            semaphore.Release();
        }
        return asset.LoadLazy;
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
                    catch
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
                    var success = assetsToStart.Writer.TryWrite(assetId);
                    Debug.Assert(success); // As the channel is unbounded it should never fail to write
                    break;
            }
        }
        return handle;
    }

    private (Guid, AssetState) GetOrCreateAssetState<TInfo, TAsset>(in TInfo info)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IAsset<TInfo>
    {
        LockSemaphore();
        try
        {
            // Determine Asset ID
            Guid assetId;
            if (TAsset.Locality is AssetLocality.Unique)
            {
                do
                {
                    assetId = Guid.NewGuid();
                } while (assets.ContainsKey(assetId)); // just paranoid...
            }
            else
                assetId = TAsset.InfoToAssetId(info);

            // Check previous asset state
            if (assets.TryGetValue(assetId, out var assetState) && assetState.RefCount > 0)
            {
                SanityCheckSharedAsset(typeof(TAsset), assetState);
                assetState.RefCount++;
                return (assetId, assetState);
            }

            // Create new asset state
            TInfo infoCopy = info;
            assetState = new()
            {
                NeedsMainThreadDisposal = TAsset.NeedsMainThreadDisposal,
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

    [Conditional("DEBUG")]
    private static void SanityCheckSharedAsset(Type expectedType, AssetState asset)
    {
        if (asset.Asset is null) return;
        var actualType = asset.Asset.GetType();
        Debug.Assert(actualType.IsAssignableTo(expectedType), "Asset type mismatch, is this a GUID conflict?");
    }

    private async Task<IDisposable> LoadAsset<TInfo, TAsset>(TInfo info, Guid assetId)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IAsset<TInfo>
    {
        // Due to AsyncLazy we can flow exceptions outside this method

        // Load asset and secondary assets
        var asset = (await TAsset.LoadAsync(this, info, Cancellation)).Asset;
        Debug.Assert(asset.Registry == this);
        CheckRegistryDisposal();

        // Propagate assets into registry state
        await LockSemaphoreAsync(Cancellation);
        try
        {
            var assetState = assets.GetValueOrDefault(assetId);
            ObjectDisposedException.ThrowIf(assetState is null or { RefCount: <= 0 }, typeof(AssetState));
        }
        catch
        {
            asset.Dispose();
            throw;
        }
        finally
        {
            semaphore.Release();
        }

        CheckRegistryDisposal();
        return asset;

        [ExcludeFromCodeCoverage] // we cannot reasonably test that, it would be a race condition
        void CheckRegistryDisposal()
        {
            if (WasDisposed)
            {
                asset.Dispose();
                ObjectDisposedException.ThrowIf(true, typeof(AssetRegistry));
            }
            Cancellation.ThrowIfCancellationRequested();
        }
    }

    public void Update() =>
        Update(MaxLowPriorityAssetsPerFrame);

    public void Update(int maxLowPrioAssets)
    {
        ObjectDisposedException.ThrowIf(WasDisposed, typeof(IAssetRegistry));
        if (!IsMainThread)
            throw new InvalidOperationException("Low batch scheduling is only allowed on the main thread");
        LockSemaphore();
        try
        {
            DisposeOldAssets();
            for (int i = 0; i < maxLowPrioAssets && assetsToStart.Reader.TryRead(out var assetId); i++)
            {
                if (assets.TryGetValue(assetId, out var assetState) &&
                    assetState.LoadLazy != NullAssetLoadLazy &&
                    !assetState.LoadLazy.IsValueCreated)
                    Task.Run(() => assetState.LoadLazy.WithCancellation(Cancellation), Cancellation);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private void DisposeOldAssets()
    {
        Debug.Assert(IsMainThread);
        Debug.Assert(semaphore.CurrentCount == 0);
        while (assetsToDispose.Reader.TryRead(out var asset))
            asset.Dispose();
    }
}
