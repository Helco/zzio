using System.Numerics;
using DefaultEcs.System;
using Silk.NET.OpenAL;

namespace zzre.game.systems;

[Without<components.Dead>()]
public sealed partial class SoundEmitter3D : AEntitySetSystem<float>
{
    private readonly OpenALDevice device;

    public SoundEmitter3D(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        diContainer.TryGetTag(out device);
        IsEnabled = diContainer.HasTag<SoundContext>();
    }

    [Update]
    private void Update(
        in components.SoundEmitter emitter,
        Location location)
    {
        device.AL.GetListenerProperty(ListenerVector3.Position, out var listenerPosition);
        var myPosition = location.LocalPosition * new Vector3(1, 1, -1);
        var distToListener = Vector3.Distance(listenerPosition, myPosition);
        float newRefDistance;
        if (emitter.MaxDistance >= distToListener)
        {
            if (emitter.ReferenceDistance < distToListener)
                newRefDistance = emitter.ReferenceDistance * (1f - (distToListener - emitter.ReferenceDistance) / (emitter.MaxDistance - emitter.ReferenceDistance));
            else
                newRefDistance = emitter.ReferenceDistance;
        }
        else
            newRefDistance = 1.1754944e-38f;

        device.AL.SetSourceProperty(emitter.SourceId, SourceFloat.ReferenceDistance, newRefDistance);
        device.AL.SetSourceProperty(emitter.SourceId, SourceVector3.Position, myPosition);
        device.AL.SetSourceProperty(emitter.SourceId, SourceVector3.Direction, location.InnerForward * new Vector3(1, 1, -1));
        device.AL.ThrowOnError();
    }
}
