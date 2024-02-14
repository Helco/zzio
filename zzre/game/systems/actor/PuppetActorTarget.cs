using System;
using DefaultEcs.System;

namespace zzre.game.systems;

public partial class PuppetActorTarget : AEntitySetSystem<float>
{
    public PuppetActorTarget(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
    }

    [Update]
    private static void Update(
        Location myLocation,
        in components.PuppetActorTarget target,
        ref components.PuppetActorMovement movement)
    {
        movement.TargetDirection = MathEx.SafeNormalize(target.Target.GlobalPosition - myLocation.GlobalPosition);
    }
}
