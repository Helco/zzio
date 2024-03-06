using DefaultEcs.System;
using zzio;
using zzre.rendering;

namespace zzre.game.systems;

public sealed partial class DistanceAlphaFade : AEntitySetSystem<float>
{
    private readonly Camera camera;

    public DistanceAlphaFade(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        camera = diContainer.GetTag<Camera>();
    }

    [Update]
    private void Update(
        Location location,
        in components.DistanceAlphaFade fade,
        ref IColor color)
    {
        var distance = location.Distance(camera.Location);
        float newAlpha = MathEx.Lerp(fade.BaseAlpha, 0f, distance, fade.MinDistance, fade.MaxDistance);
        color = color with { a = (byte)(newAlpha * 255f) };
    }
}
