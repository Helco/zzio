using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DefaultEcs.System;
using Silk.NET.OpenAL;

namespace zzre.game.systems;

public sealed unsafe class SoundContext : ISystem<float>
{
    private readonly OpenALDevice device;
    private readonly SwitchBackScope switchBackScope;
    private readonly List<uint> deferredBufferDisposals = new(32);
    private Context* context,
        prevContext = null; // in case we need to switch contexts briefly
    private bool isEnabled;

    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (isEnabled == value || context is null)
                return;
            isEnabled = value;
            if (value)
                device.ALC.SuspendContext(context);
            else
                device.ALC.ProcessContext(context);
        }
    }

    public SoundContext(ITagContainer diContainer)
    {
        switchBackScope = new(this);
        if (!(isEnabled = diContainer.TryGetTag(out device)))
            return;

        context = device.ALC.CreateContext(device.Device, attributeList: null);
        if (context == null)
        {
            device.Logger.Error("Could not create context");
            isEnabled = false;
        }
        else
        {
            isEnabled = true;
            diContainer.AddTag(this); // to show other systems that sound is indeed alive
        }

        using var _ = EnsureIsCurrent();
        device.AL.DistanceModel(DistanceModel.InverseDistanceClamped);
        device.AL.SetListenerProperty(ListenerFloat.Gain, 1f);
    }

    public void Dispose()
    {
        if (context == null)
            return;
        if (deferredBufferDisposals.Count > 0)
        {
            using var _ = EnsureIsCurrent();
            DisposeDeferredBuffers();
        }
        if (device.ALC.GetCurrentContext() == context)
            device.ALC.MakeContextCurrent(null);
        device.ALC.DestroyContext(context);
        context = null;
        isEnabled = false;
        device.Logger.Debug("Disposing SoundContext");
    }

    public void Update(float _)
    {
        if (!isEnabled || context is null)
            return;
        if (!device.ALC.MakeContextCurrent(context))
        {
            device.Logger.Error("Could not make context current");
            isEnabled = false;
        }
        DisposeDeferredBuffers();
        device.AL.ThrowOnError();
    }

    // This should only ever be necessary for handling messages
    public IDisposable? EnsureIsCurrent()
    {
        prevContext = device.ALC.GetCurrentContext();
        if (context == prevContext)
            return null;
        if (!device.ALC.MakeContextCurrent(context))
            device.Logger.Error("Could not ensure current context");
        return prevContext == null ? null : switchBackScope;
    }

    public void AddBufferDisposal(uint bufferId)
    {
        lock(deferredBufferDisposals)
            deferredBufferDisposals.Add(bufferId);
    }

    // make sure the context is current before calling this
    private void DisposeDeferredBuffers()
    {
        lock (deferredBufferDisposals)
        {
            if (deferredBufferDisposals.Count > 0)
            {
                var buffers = CollectionsMarshal.AsSpan(deferredBufferDisposals);
                fixed (uint* bufferPtr = buffers)
                    device.AL.DeleteBuffers(buffers.Length, bufferPtr);
                deferredBufferDisposals.Clear();
            }
        }
    }

    private sealed class SwitchBackScope : IDisposable
    {
        private readonly SoundContext parent;
        public SwitchBackScope(SoundContext parent) => this.parent = parent;
        public void Dispose()
        {
            if (parent.prevContext is null)
                return;
            if (parent.prevContext != parent.context &&
                !parent.device.ALC.MakeContextCurrent(parent.prevContext))
                parent.device.Logger.Error("Could not switch back current context");
            parent.prevContext = null;
        }
    }
}
