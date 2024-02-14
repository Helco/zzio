using System;
using DefaultEcs.System;

namespace zzre.game.systems;

public partial class MoveToLocation : AEntitySetSystem<float>
{
    public MoveToLocation(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
    }

    [Update]
    public static void Update(
        Location location,
        in components.MoveToLocation move)
    {
        location.LocalPosition = move.Parent.GlobalPosition + move.RelativePosition;
    }
}
