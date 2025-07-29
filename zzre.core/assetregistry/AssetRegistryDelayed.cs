using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using DotNext.Threading;

namespace zzre;

public sealed class AssetRegistryDelayed(IAssetRegistry Inner) : IAssetRegistryInternal
{
    public bool WasDisposed => Inner.WasDisposed;
    public bool IsMainThread => Inner.IsMainThread;
    public ITagContainer DIContainer => Inner.DIContainer;
    public IAssetRegistry? ParentRegistry => Inner.ParentRegistry;
    public bool IsLocalRegistry => Inner.IsLocalRegistry;
    public CancellationToken Cancellation => Inner.Cancellation;
    public AssetRegistryStats Stats => Inner.Stats;

    public void CopyDebugInfo(List<IAssetRegistry.AssetInfo> assetInfos) =>
        Inner.CopyDebugInfo(assetInfos);

    public void Dispose() => Inner.Dispose();
    public void Update() => Inner.Update();

    void IAssetRegistryInternal.AddRef(Guid assetId) =>
        ((IAssetRegistryInternal)Inner).AddRef(assetId);

    public void Apply<TAsset>(AssetHandle<TAsset> handle, Action<AssetHandle<TAsset>> action)
        where TAsset : class, IAsset =>
        Inner.Apply(handle, action);

    void IAssetRegistryInternal.CheckType(Guid assetId, Type type) =>
        ((IAssetRegistryInternal)Inner).CheckType(assetId, type);

    AsyncLazy<IDisposable> IAssetRegistryInternal.GetAsset(Guid assetId) =>
        ((IAssetRegistryInternal)Inner).GetAsset(assetId);

    public bool TryGet<TAsset>(Guid assetId, out AssetHandle<TAsset> handle)
        where TAsset : class, IAsset =>
        Inner.TryGet(assetId, out handle);

    public bool DelayDeletion
    {
        get => Volatile.Read(ref delayDeletion);
        set
        {
            Volatile.Write(ref delayDeletion, value);
            if (!value || Inner.WasDisposed)
                return;
            lock (assetIdsToDelete)
            {
                foreach (var id in assetIdsToDelete)
                    ((IAssetRegistryInternal)Inner).DelRef(id);
                assetIdsToDelete.Clear();
            }
        }
    }

    private readonly List<Guid> assetIdsToDelete = new(64);
    private bool delayDeletion;

    public AssetHandle<TAsset> Load<TInfo, TAsset>(in TInfo info, AssetPriority priority)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IAsset<TInfo>
    {
        var parentHandle = Inner.Load<TInfo, TAsset>(info, priority);
        Debug.Assert(parentHandle.Asset == parentHandle.Asset); // checks that the handle is not disposed
        return new(this, parentHandle.AssetId, false);
    }

    void IAssetRegistryInternal.DelRef(Guid assetId)
    {
        if (DelayDeletion)
        {
            lock (assetIdsToDelete)
                assetIdsToDelete.Add(assetId);
        }
        else
            ((IAssetRegistryInternal)Inner).DelRef(assetId);
    }
}
