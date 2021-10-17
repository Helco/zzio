using System;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.System;

namespace zzre.game.systems
{
    public partial class Butterfly : AEntitySetSystem<float>
    {
        private const float AngleSpeed = 40f * MathF.PI / 180f;

        public Butterfly(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, null, 0)
        {
        }

        [Update]
        private void Update(float elapsedTime, in Location location, ref components.Butterfly butterfly)
        {
            var angleDelta = elapsedTime * AngleSpeed;
            butterfly.Angle += angleDelta;
            if (butterfly.Angle > 2 * MathF.PI)
            {
                angleDelta -= 2 * MathF.PI - angleDelta;
                butterfly.RotateDir *= -1f;
                butterfly.Angle = 0f;
            }

            location.LocalRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, angleDelta * butterfly.RotateDir);
            location.LocalPosition += location.GlobalForward * butterfly.Speed * elapsedTime;
        }
    }
}
