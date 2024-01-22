using System;
using System.Numerics;
using DefaultEcs.System;

namespace zzre.game.systems;

public partial class CirclingBird : AEntitySetSystem<float>
{
    public CirclingBird(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
    }

    [Update]
    private void Update(float elapsedTime, in Location location, in components.CirclingBird bird)
    {
        var moveSin = MathF.Sin(elapsedTime * bird.Speed);
        var moveCos = MathF.Cos(elapsedTime * bird.Speed);
        var deltaPos = location.LocalPosition - bird.Center;
        var newPos = bird.Center + new Vector3(
            moveCos * deltaPos.X - moveSin * deltaPos.Z,
            0f,
            moveSin * deltaPos.X + moveCos * deltaPos.Z);

        location.LookAt(newPos);
        location.LocalPosition = newPos;
    }
}
