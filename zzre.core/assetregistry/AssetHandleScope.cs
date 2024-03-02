using System;
using System.Collections.Generic;

namespace zzre;

public sealed class AssetHandleScope(IAssetRegistry registry) : IAssetRegistry
{
    // we use a dictionary to keep handles to the same asset from piling up 
    // wasting memory and cycles at disposal time
    private readonly Dictionary<Guid, IAssetRegistryInternal> handlesToDispose = new(128);
    private bool delayDisposals;

    IAssetRegistryInternal IAssetRegistry.InternalRegistry => Registry.InternalRegistry;
    public IAssetRegistry Registry => registry;
    public AssetRegistryStats Stats => registry.Stats;
    public ITagContainer DIContainer => Registry.DIContainer;
    public bool DelayDisposals
    {
        get => delayDisposals;
        set
        {
            delayDisposals = value;
            if (!value)
            {
                foreach (var (assetId, registryInternal) in handlesToDispose)
                    registryInternal.DisposeHandle(new(registryInternal, this, assetId));
                handlesToDispose.Clear();
            }
        }
    }

    public unsafe AssetHandle Load<TInfo, TApplyContext>(
        in TInfo info,
        AssetLoadPriority priority,
        delegate* managed<AssetHandle, ref readonly TApplyContext, void> applyFnptr,
        in TApplyContext applyContext)
        where TInfo : IEquatable<TInfo>
    {
        var handle = registry.Load(info, priority, applyFnptr, applyContext);
        return new(this, handle.AssetID);
    }

    public AssetHandle Load<TInfo>(
        in TInfo info,
        AssetLoadPriority priority,
        Action<AssetHandle>? applyAction = null)
        where TInfo : IEquatable<TInfo>
    {
        var handle = registry.Load(info, priority, applyAction);
        return new(this, handle.AssetID);
    }

    internal void DisposeHandle(AssetHandle handle)
    {
        if (!DelayDisposals ||
            !handlesToDispose.TryAdd(handle.AssetID, handle.registryInternal))
            handle.registryInternal.DisposeHandle(handle);
    }

    public void ApplyAssets() { } // it is just a scope

    public void Dispose()
    {
        DelayDisposals = false;
    }
}
