using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace zzre;

public sealed class AssetRegistryDelayed : IAssetRegistryInternal
{
    [ExcludeFromCodeCoverage]
    public bool WasDisposed => Inner.WasDisposed;
    [ExcludeFromCodeCoverage]
    public bool IsMainThread => Inner.IsMainThread;
    [ExcludeFromCodeCoverage]
    public ITagContainer DIContainer => Inner.DIContainer;
    [ExcludeFromCodeCoverage]
    public IAssetRegistry? ParentRegistry => Inner.ParentRegistry;
    [ExcludeFromCodeCoverage]
    public bool IsLocalRegistry => Inner.IsLocalRegistry;
    [ExcludeFromCodeCoverage]
    public CancellationToken Cancellation => Inner.Cancellation;
    [ExcludeFromCodeCoverage]
    public AssetRegistryStats Stats => Inner.Stats;

    [ExcludeFromCodeCoverage]
    public void CopyDebugInfo(List<IAssetRegistry.AssetInfo> assetInfos) =>
        Inner.CopyDebugInfo(assetInfos);

    [ExcludeFromCodeCoverage]
    public void Dispose() => Inner.Dispose();
    [ExcludeFromCodeCoverage]
    public void Update() => Inner.Update();

    [ExcludeFromCodeCoverage]
    void IAssetRegistryInternal.AddRef(Guid assetId) =>
        ((IAssetRegistryInternal)Inner).AddRef(assetId);

    [ExcludeFromCodeCoverage]
    void IAssetRegistryInternal.DelRef(Guid assetId) =>
        ((IAssetRegistryInternal)Inner).DelRef(assetId);

    [ExcludeFromCodeCoverage]
    public void Apply<TAsset>(AssetHandle<TAsset> handle, Action<AssetHandle<TAsset>> action)
        where TAsset : class, IAsset => Inner.Apply(handle, action);

    [ExcludeFromCodeCoverage]
    void IAssetRegistryInternal.CheckType(Guid assetId, Type type) =>
        ((IAssetRegistryInternal)Inner).CheckType(assetId, type);

    [ExcludeFromCodeCoverage]
    FFTask<IDisposable> IAssetRegistryInternal.GetAsset(Guid assetId) =>
        ((IAssetRegistryInternal)Inner).GetAsset(assetId);

    [ExcludeFromCodeCoverage]
    public bool TryGet<TAsset>(Guid assetId, out AssetHandle<TAsset> handle) where TAsset : class, IAsset =>
        Inner.TryGet(assetId, out handle);

    public readonly IAssetRegistry Inner;
    private readonly HashSet<AssetHandle> assetIdsToDelete = new(64);

    public AssetRegistryDelayed(IAssetRegistry inner)
    {
        Inner = inner;
    }

    public AssetHandle<TAsset> Load<TInfo, TAsset>(in TInfo info, AssetPriority priority)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IAsset<TInfo>
    {
        var parentHandle = Inner.Load<TInfo, TAsset>(info, priority);
        Debug.Assert(parentHandle.Asset == parentHandle.Asset); // checks that the handle is not disposed
        lock (assetIdsToDelete)
        {
            var unsafeCopy = new AssetHandle(parentHandle.Registry, parentHandle.AssetId);
            if (assetIdsToDelete.Add(unsafeCopy))
                unsafeCopy.Duplicate(); // increment the reference count only if we have to
        }
        return parentHandle;
    }

    public void DisposeDelayedAssets()
    {
        lock (assetIdsToDelete)
        {
            foreach (var handle in assetIdsToDelete)
                handle.Dispose();
            assetIdsToDelete.Clear();
        }
    }
}
