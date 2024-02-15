using System.Numerics;
using DefaultEcs.System;
using Silk.NET.OpenAL;

namespace zzre.game.systems;

[With<components.SoundListener>]
public sealed partial class SoundListener : AEntitySetSystem<float>
{
    private readonly OpenALDevice device = null!;

    public SoundListener(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        World.SetMaxCapacity<components.SoundListener>(1);
        if (IsEnabled = diContainer.HasTag<SoundContext>())
            device = diContainer.GetTag<OpenALDevice>();
    }

    [Update]
    private unsafe void Update(Location location)
    {
        device.AL.SetListenerProperty(ListenerVector3.Position, location.LocalPosition * -Vector3.UnitZ);
        var orientation = stackalloc Vector3[2];
        orientation[0] = location.InnerForward * -Vector3.UnitZ;
        orientation[1] = location.InnerUp * -Vector3.UnitZ;
        device.AL.SetListenerProperty(ListenerFloatArray.Orientation, (float*)orientation);
    }
}
