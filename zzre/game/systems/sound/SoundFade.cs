using DefaultEcs.System;

namespace zzre.game.systems;

public sealed partial class SoundFade : AEntitySetSystem<float>
{
    private readonly OpenALDevice device;

    public SoundFade(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        diContainer.TryGetTag(out device);
        IsEnabled = diContainer.HasTag<SoundContext>();
    }

    [Update]
    private void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        in components.SoundEmitter emitter,
        ref components.SoundFade fade)
    {
        if (fade.Delay > 0f)
        {
            fade.Delay -= elapsedTime;
            if (fade.Delay > 0f)
                return;
        }
        fade.Time += elapsedTime;
        float newVolume = MathEx.Lerp(fade.FromVolume, fade.ToVolume, fade.Time, 0f, fade.Length);
        device.AL.SetListenerProperty(Silk.NET.OpenAL.ListenerFloat.Gain, newVolume);
        if (fade.Time >= fade.Length)
            entity.Set<components.Dead>();
        device.AL.ThrowOnError();
    }
}
