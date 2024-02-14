using System;
using DefaultEcs.System;
using DefaultEcs.Command;

namespace zzre.game.systems.ui;

internal sealed partial class Fade : AEntitySetSystem<float>
{
    private readonly EntityCommandRecorder recorder;

    public Fade(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        recorder = diContainer.GetTag<EntityCommandRecorder>();
    }

    [Update]
    private void Update(in DefaultEcs.Entity entity, float elapsedTime, ref zzio.IColor color, ref components.ui.Fade flashFade)
    {
        flashFade.CurrentTime += elapsedTime;
        color = color with { a = (byte)(flashFade.Value * 255f) };
        if (flashFade.IsFinished)
            recorder.Record(entity).Dispose();
    }
}
