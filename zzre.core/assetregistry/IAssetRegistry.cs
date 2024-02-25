using System;
namespace zzre;

public enum AssetLoadPriority
{
    Synchronous,
    High,
    Low
}

public interface IAssetRegistry : IDisposable
{
    ITagContainer DIContainer { get; }
    AssetRegistryStats Stats { get; }

    unsafe AssetHandle Load<TInfo, TApplyContext>(
        in TInfo info,
        AssetLoadPriority priority,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
        where TInfo : IEquatable<TInfo>;

    AssetHandle Load<TInfo>(
        in TInfo info,
        AssetLoadPriority priority,
        Action<AssetHandle>? applyAction = null)
        where TInfo : IEquatable<TInfo>;

    void Unload(AssetHandle handle);

    void ApplyAssets();
}
