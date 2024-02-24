using System;
using System.Collections.Generic;

namespace zzre;

public class AssetHandleScope(AssetRegistry registry) : IAssetHandleScope
{
    private readonly List<AssetHandle> handlesToDispose = new(64);
    private bool delayDisposals;

    public AssetRegistry Registry => registry;
    public bool DelayDisposals
    {
        get => delayDisposals;
        set
        {
            delayDisposals = value;
            if (value)
            {
                foreach (var handle in handlesToDispose)
                    handle.Dispose();
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
        Action<AssetHandle> applyAction)
        where TInfo : IEquatable<TInfo>
    {
        var handle = registry.Load(info, priority, applyAction);
        return new(this, handle.AssetID);
    }

    public void Unload(AssetHandle handle)
    {
        if (DelayDisposals)
            handlesToDispose.Add(handle);
        else
            handle.Dispose();
    }

    public void Dispose()
    {
        DelayDisposals = false;
    }
}
