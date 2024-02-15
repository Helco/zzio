using DefaultEcs.System;
using Silk.NET.OpenAL;

namespace zzre.game.systems;

public sealed partial class SoundStoppedEmitter : AEntitySetSystem<float>
{
    private readonly OpenALDevice device;

    public SoundStoppedEmitter(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        diContainer.TryGetTag(out device);
    }

    [Update]
    private void Update(
        in DefaultEcs.Entity entity,
        in components.SoundEmitter emitter)
    {
        device.AL.GetSourceProperty(emitter.SourceId, GetSourceInteger.SourceState, out var state);
        if (state == (int)SourceState.Stopped)
            entity.Set<components.Dead>();
    }
}
