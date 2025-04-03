using System;
using System.Collections.Generic;

namespace zzre;

/// <summary>Enables additional, not-particularly-efficient access to the state of registries for debugging purposes</summary>
public interface IAssetRegistryDebug : IAssetRegistry
{
    /// <summary>A snapshot of an assets state</summary>
    /// <param name="ID">The asset ID</param>
    /// <param name="Type">The type of the asset instance</param>
    /// <param name="Name">The debugging name of the asset</param>
    /// <param name="RefCount">The reference count of the asset</param>
    /// <param name="State">The loading state of the asset</param>
    /// <param name="Priority">The *effective* load priority of the asset</param>
    public readonly record struct AssetInfo(
        Guid ID,
        Type Type,
        string Name,
        int RefCount,
        AssetState State,
        AssetLoadPriority Priority);

    /// <summary>Whether this registry was already disposed</summary>
    bool WasDisposed { get; }
    /// <summary>Creates a snapshot of the state of all, currently registered assets</summary>
    /// <param name="assetInfos">The asset states will be copied into this list</param>
    void CopyDebugInfo(List<AssetInfo> assetInfos);
}

public partial class AssetRegistry : IAssetRegistryDebug
{
    void IAssetRegistryDebug.CopyDebugInfo(List<IAssetRegistryDebug.AssetInfo> assetInfos)
    {
        lock (assets)
        {
            assetInfos.Clear();
            assetInfos.EnsureCapacity(assetInfos.Count + assets.Values.Count);
            foreach (var asset in assets.Values)
            {
                assetInfos.Add(new(
                    asset.ID,
                    asset.GetType(),
                    asset.ToString() ?? "",
                    asset.RefCount,
                    asset.State,
                    asset.Priority));
            }
        }
    }
}
