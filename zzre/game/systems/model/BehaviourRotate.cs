using System.Numerics;
using DefaultEcs.System;

namespace zzre.game.systems
{
    [PauseDuringUIScreen] // actually only xrotate -_-
    public partial class BehaviourRotate : AEntitySetSystem<float>
    {
        public BehaviourRotate(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
        {
        }

        [Update]
        private void Update(float elapsedTime, Location location, in components.behaviour.Rotate rotate)
        {
            location.LocalRotation *= Quaternion.CreateFromAxisAngle(
                rotate.Axis,
                elapsedTime * rotate.Speed * MathEx.DegToRad);
        }
    }
}
