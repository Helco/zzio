using DefaultEcs.System;
using zzre.rendering;

namespace zzre.game.systems;

[With(typeof(components.FairyDistanceVisibility))]
public sealed partial class FairyVisibilityByDistance : AEntitySetSystem<float>
{
    [Configuration(Description = "Minimal distance for enemy fairies to be visible")]
    private float EnemyMinDistance = 0.5f;

    private readonly Camera camera;

    public FairyVisibilityByDistance(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        camera = diContainer.GetTag<Camera>();
    }

    [Update]
    private void Update(
        Location location,
        in components.ActorParts actorParts,
        in components.Parent parent)
    {
        var distance = location.DistanceSquared(camera.Location);
        var newVisibility = distance > EnemyMinDistance * EnemyMinDistance
            ? components.Visibility.Visible
            : components.Visibility.Invisible;
        actorParts.Body.Set(newVisibility);
        actorParts.Wings?.Set(newVisibility);
    }
}
