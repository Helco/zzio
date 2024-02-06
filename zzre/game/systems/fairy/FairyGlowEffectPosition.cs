using System;
using DefaultEcs.System;
using zzre.rendering;

namespace zzre.game.systems;

[With(typeof(components.FairyGlowEffectPosition))]
public partial class FairyGlowEffectPosition : AEntitySetSystem<float>
{
    private readonly Camera camera;

    public FairyGlowEffectPosition(ITagContainer diContainer)
        : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        camera = diContainer.GetTag<Camera>();
    }

    [Update]
    private void Update(
        in components.Parent parent,
        Location location)
    {
        location.LocalPosition = parent.Entity.Get<Location>().GlobalPosition -
            0.1f * camera.Location.GlobalForward;
    }
}
