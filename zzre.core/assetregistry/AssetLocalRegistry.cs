using System;
using System.Collections.Generic;

namespace zzre;

/// <summary>A local registry to enable loading of local assets</summary>
public sealed class AssetLocalRegistry : zzio.BaseDisposable, IAssetRegistryDebug
{
    private readonly IAssetRegistry globalRegistry;
    private readonly AssetRegistry localRegistry;
    private readonly AssetHandleScope localScope = new(null!); // the scope will not be used to load, null is a canary value for this

    IAssetRegistryInternal IAssetRegistry.InternalRegistry => localRegistry;
    /// <inheritdoc/>
    public ITagContainer DIContainer => localRegistry.DIContainer;

    /// <inheritdoc cref="AssetHandleScope.DelayDisposals"/>
    public bool DelayDisposals
    {
        get => localScope.DelayDisposals;
        set => localScope.DelayDisposals = value;
    }

    /// <inheritdoc/>
    public AssetRegistryStats Stats => globalRegistry.Stats + localRegistry.Stats;
    /// <inheritdoc/>
    public AssetRegistryStats LocalStats => localRegistry.Stats;

    /// <summary>Constructs a new local registry</summary>
    /// <param name="debugName">A name for debugging purposes (used in logs)</param>
    /// <param name="diContainer">The <see cref="ITagContainer"/> used for loading asset contents</param>
    public AssetLocalRegistry(string debugName, ITagContainer diContainer)
    {
        globalRegistry = diContainer.GetTag<IAssetRegistry>();
        if (globalRegistry is not IAssetRegistryInternal { IsLocalRegistry: false })
            throw new ArgumentException("Registry given to local registry is not a global registry");
        localRegistry = new AssetRegistry(debugName, diContainer, this);
    }

    private IAssetRegistry RegistryFor<TInfo>() where TInfo : IEquatable<TInfo> =>
        AssetInfoRegistry<TInfo>.Locality == AssetLocality.Global ? globalRegistry : localRegistry;

    /// <inheritdoc/>
    public unsafe AssetHandle Load<TInfo, TApplyContext>(
        in TInfo info,
        AssetLoadPriority priority,
        delegate*<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
        where TInfo : IEquatable<TInfo>
    {
        var registry = RegistryFor<TInfo>();
        var handle = registry.Load(in info, priority, applyFnptr, in applyContext);
        return new(registry, localScope, handle.AssetID);
    }

    /// <inheritdoc/>
    public AssetHandle Load<TInfo>(
        in TInfo info,
        AssetLoadPriority priority,
        Action<AssetHandle>? applyAction = null)
        where TInfo : IEquatable<TInfo>
    {
        var registry = RegistryFor<TInfo>();
        var handle = registry.Load(in info, priority, applyAction);
        return new(registry, localScope, handle.AssetID);
    }

    /// <inheritdoc/>
    public void ApplyAssets() => localRegistry.ApplyAssets();

    void IAssetRegistryDebug.CopyDebugInfo(List<IAssetRegistryDebug.AssetInfo> assetInfos) =>
        (localRegistry as IAssetRegistryDebug).CopyDebugInfo(assetInfos);

    protected override void DisposeManaged()
    {
        localScope?.Dispose();
        localRegistry?.Dispose();
    }
}
