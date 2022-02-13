using System;
using DefaultEcs.System;
using DefaultEcs.Command;
using zzio;

namespace zzre.game.systems.ui
{
    public partial class Fade : AEntitySetSystem<float>
    {
        private readonly EntityCommandRecorder recorder;

        public Fade(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
        {
            recorder = diContainer.GetTag<EntityCommandRecorder>();
        }

        [Update]
        private void Update(in DefaultEcs.Entity entity, float elapsedTime, ref IColor color, ref components.ui.Fade fade)
        {
            color = color with { a = (byte)(fade.Value * 255f) };
            fade = fade with { Time = Math.Min(fade.Time + elapsedTime, fade.Duration) };
            if (fade.Time == fade.Duration)
                recorder.Record(entity).Remove<components.ui.Fade>();
        }
    }
}
