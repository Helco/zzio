using DefaultEcs.System;
using Silk.NET.OpenAL;

namespace zzre.game.systems;

public sealed unsafe class SoundContext : ISystem<float>
{
    private readonly OpenALDevice device;
    private Context* context;
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
        if (!diContainer.TryGetTag(out device))
        {
            isEnabled = false;
            return;
        }

        context = device.ALC.CreateContext(device.Device, attributeList: null);
        if (context == null)
        {
            device.Logger.Error("Could not create context");
            isEnabled = false;
        }

        isEnabled = true;
        diContainer.AddTag(diContainer); // to show other systems that sound is indeed alive
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
        if (device.ALC.MakeContextCurrent(context))
        {
            device.Logger.Error("Could not make context current");
            isEnabled = false;
        }
    }
}
