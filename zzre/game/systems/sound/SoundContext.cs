using System;
using DefaultEcs.System;
using Silk.NET.OpenAL;

namespace zzre.game.systems;

public sealed unsafe class SoundContext : ISystem<float>
{
    private readonly OpenALDevice device;
    private readonly SwitchBackScope switchBackScope;
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
            diContainer.AddTag(diContainer); // to show other systems that sound is indeed alive
        }
    }

    public void Dispose()
    {
        if (context == null)
            return;
        device.ALC.DestroyContext(context);
        context = null;
        isEnabled = false;
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
        device.AL.DistanceModel(DistanceModel.InverseDistanceClamped);
    }

    // This should only ever be necessary for handling messages
    public IDisposable? EnsureIsCurrent()
    {
        prevContext = device.ALC.GetCurrentContext();
        return context == prevContext || prevContext == null ? null : switchBackScope;
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
