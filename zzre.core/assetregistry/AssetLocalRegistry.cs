using System;

namespace zzre;

public sealed class AssetLocalRegistry : IAssetRegistry
{
    private readonly IAssetRegistry globalRegistry;
    private readonly AssetRegistry localRegistry;
    private readonly AssetHandleScope localScope = new(null!); // the scope will not be used to load, null is a canary value for this
    private bool disposedValue;

    public ITagContainer DIContainer => localRegistry.DIContainer;

    public bool DelayDisposals
    {
        get => localScope.DelayDisposals;
        set => localScope.DelayDisposals = value;
    }

    public AssetLocalRegistry(string debugName, ITagContainer diContainer)
    {
        globalRegistry = diContainer.GetTag<IAssetRegistry>();
        if (globalRegistry is not AssetRegistry { IsLocalRegistry: false })
            throw new ArgumentException("Registry given to local registry is not a global registry");
        localRegistry = new AssetRegistry(debugName, diContainer, isLocalRegistry: true);
    }

    private IAssetRegistry RegistryFor<TInfo>() where TInfo : IEquatable<TInfo> =>
        AssetInfoRegistry<TInfo>.IsLocal ? localRegistry : globalRegistry;

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

    public void Unload(AssetHandle handle) =>
        localScope.Unload(handle);

    public void ApplyAssets() => localRegistry.ApplyAssets();

    private void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                localScope?.Dispose();
                localRegistry?.Dispose();
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
