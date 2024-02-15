using System.Numerics;
using DefaultEcs.System;
using Silk.NET.OpenAL;

namespace zzre.game.systems;

public sealed partial class SoundEmitter : AEntitySetSystem<float>
{
    private readonly OpenALDevice device;

    public SoundEmitter(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        diContainer.TryGetTag(out device);
    }

    [Update]
    private void Update(
        in components.SoundEmitter emitter,
        Location location)
    {
        device.AL.SetSourceProperty(emitter.SourceId, SourceVector3.Position, location.LocalPosition * -Vector3.UnitZ);
        device.AL.SetSourceProperty(emitter.SourceId, SourceVector3.Direction, location.InnerForward * -Vector3.UnitZ);
    }
}
