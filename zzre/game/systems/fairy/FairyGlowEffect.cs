using DefaultEcs.System;
using zzre.rendering;

namespace zzre.game.systems;

[With(typeof(components.FairyGlowEffectPosition))]
public partial class FairyGlowEffect : AEntitySetSystem<float>
{
    private readonly Camera camera;

    public FairyGlowEffect(ITagContainer diContainer)
        : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        camera = diContainer.GetTag<Camera>();
    }

    [Update]
    private void Update(
        in DefaultEcs.Entity entity,
        in components.Parent parent,
        ref components.Visibility visibility,
        Location location)
    {
        if (!parent.Entity.TryGet(out Location parentLocation))
            return;

        location.LocalPosition = parentLocation.GlobalPosition - 0.1f * camera.Location.GlobalForward;
        if (parent.Entity.TryGet<components.ActorParts>(out var parts) &&
            parts.Body.TryGet<components.Visibility>(out var parentVisibility) &&
            parentVisibility != visibility)
        {
            visibility = parentVisibility;
            entity.NotifyChanged<components.Visibility>();
        }
    }
}
