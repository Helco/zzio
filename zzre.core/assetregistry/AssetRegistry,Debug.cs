using System;
using System.Collections.Generic;

namespace zzre;

public interface IAssetRegistryDebug : IAssetRegistry
{
    public readonly record struct AssetInfo(
        Guid ID,
        Type Type,
        string Name,
        int RefCount,
        AssetState State,
        AssetLoadPriority Priority);

    bool WasDisposed { get; }
    void CopyDebugInfo(List<AssetInfo> assetInfos);
}

partial class AssetRegistry : IAssetRegistryDebug
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
