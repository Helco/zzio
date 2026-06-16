using System;
using System.Collections.Generic;
using System.Threading;

namespace zzre;

public enum AssetPriority
{
    Synchronous,
    High,
    Low
}

public interface IAssetRegistry : IDisposable
{
    bool WasDisposed { get; }
    bool IsMainThread { get; }
    ITagContainer DIContainer { get; }
    IAssetRegistry? ParentRegistry { get; }
    bool IsLocalRegistry { get; }
    CancellationToken Cancellation { get; } // is triggered when registry is disposed
    AssetRegistryStats Stats { get; }

    AssetHandle<TAsset> Load<TInfo, TAsset>(in TInfo info, AssetPriority priority)
        where TInfo : struct, IEquatable<TInfo>
        where TAsset : class, IAsset<TInfo>;

    void Apply<TAsset>(AssetHandle<TAsset> handle, Action<AssetHandle<TAsset>> action)
        where TAsset : class, IAsset;

    bool TryGet<TAsset>(Guid assetId, out AssetHandle<TAsset> handle)
        where TAsset : class, IAsset;

    void Update();

    /// <summary>A snapshot of an assets state</summary>
    /// <param name="ID">The asset ID</param>
    /// <param name="Type">The type of the asset instance</param>
    /// <param name="Name">The debugging name of the asset</param>
    /// <param name="RefCount">The reference count of the asset</param>
    /// <param name="IsLoaded">The loading state of the asset</param>
    /// <param name="Priority">The *effective* load priority of the asset</param>
    public readonly record struct AssetInfo(
        Guid ID,
        Type Type,
        string Name,
        int RefCount,
        bool IsLoaded,
        AssetPriority Priority);

    /// <summary>Creats a snapshot of the state of all, currently registered assets</summary>
    /// <param name="assetInfos">The asset states will be copied into this list</param>
    void CopyDebugInfo(List<AssetInfo> assetInfos);
}

internal interface IAssetRegistryInternal : IAssetRegistry
{
    void AddRef(Guid assetId);
    void DelRef(Guid assetId);
    FFTask<IDisposable> GetAsset(Guid assetId);
    void CheckType(Guid assetId, Type type);
}
