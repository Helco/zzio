﻿using Veldrid;
using zzio;

namespace zzre.rendering;

public abstract class BaseBinding : BaseDisposable
{
    protected bool isBindingDirty;

    public IMaterial Parent { get; }
    public abstract BindableResource? Resource { get; }

    protected BaseBinding(IMaterial parent)
    {
        Parent = parent;
    }

    public bool ResetIsDirty()
    {
        var result = isBindingDirty;
        isBindingDirty = false;
        return result;
    }

    public abstract void Update(CommandList cl);
}
