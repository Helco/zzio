using System;
using System.Collections.Generic;

namespace zzre;

/// <summary>An asset handle scope can be used to delay disposal of asset handles until a better point in time</summary>
/// <param name="registry">The registry assets are loaded and disposed at</param>
public sealed class AssetHandleScope(IAssetRegistry registry) : IAssetRegistry
{
    // we use a dictionary to keep handles to the same asset from piling up 
    // wasting memory and cycles at disposal time
    private readonly Dictionary<Guid, IAssetRegistryInternal> handlesToDispose = new(128);
    private bool delayDisposals;

    IAssetRegistryInternal IAssetRegistry.InternalRegistry => Registry.InternalRegistry;
    /// <summary>The registry the handle scope uses for loading and disposal</summary>
    public IAssetRegistry Registry => registry;
    /// <summary>The <see cref="AssetRegistryStats"/> of the underlying registry</summary>
    public AssetRegistryStats Stats => registry.Stats;
    /// <summary>The <see cref="ITagContainer"/> of the underlying registry</summary>
    public ITagContainer DIContainer => Registry.DIContainer;

    /// <summary>Whether disposal of handles returned by this <see cref="AssetHandleScope"/> are executed</summary>
    /// <remarks>Setting this property to <c>false</c> will trigger all outstanding disposals</remarks>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <summary>No-op as the underlying registry is supposed to apply the assets itself</summary>
    public void ApplyAssets() { } // it is just a scope

    /// <inheritdoc/>
    public void Dispose()
    {
        DelayDisposals = false;
    }
}
