using DefaultEcs.System;

namespace zzre.game.systems;

public sealed partial class SoundFade : AEntitySetSystem<float>
{
    public SoundFade(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        IsEnabled = diContainer.HasTag<SoundContext>();
    }

    [Update]
    private static void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        ref components.SoundEmitter emitter,
        ref components.SoundFade fade)
    {
        if (fade.Delay > 0f)
        {
            fade.Delay -= elapsedTime;
            if (fade.Delay > 0f)
                return;
        }
        fade.Time += elapsedTime;
        emitter.Volume = MathEx.Lerp(fade.FromVolume, fade.ToVolume, fade.Time, 0f, fade.Length);
        if (fade.Time >= fade.Length)
            entity.Set<components.Dead>();
    }
}
