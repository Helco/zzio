using System;
using System.Numerics;
using DefaultEcs.System;

namespace zzre.game.systems
{
    [PauseDuringUIScreen]
    public partial class BehaviourMagicBridge : AEntitySetSystem<float>
    {
        private const float Speed = 0.6f;

        public BehaviourMagicBridge(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
        {
        }

        [Update]
        private void Update(
            float elapsedTime,
            Location location,
            ref components.behaviour.MagicBridge bridge)
        {
            location.LocalPosition += Vector3.UnitY * elapsedTime * MathF.Sin(bridge.Age) * bridge.Amplitude;
            bridge.Age += elapsedTime * Speed;
        }
    }
}
