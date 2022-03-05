using System;
using System.Numerics;
using DefaultEcs.System;

namespace zzre.game.systems
{
    [PauseDuring(PauseTrigger.UIScreen)]
    public partial class BehaviourSwing : AEntitySetSystem<float>
    {
        private const float Speed = 2f;
        private const float XRotation = 0.05f;
        private const float YRotation = 0.1f;

        private readonly GameTime time;
        private readonly IDisposable addedSubscription;

        public BehaviourSwing(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
        {
            time = diContainer.GetTag<GameTime>();
            addedSubscription = World.SubscribeComponentAdded<components.behaviour.Swing>(HandleComponentAdded);
        }

        public override void Dispose()
        {
            base.Dispose();
            addedSubscription.Dispose();
        }

        private void HandleComponentAdded(in DefaultEcs.Entity entity, in components.behaviour.Swing swing)
        {
            var location = entity.Get<Location>();
            entity.Set(swing with { StartRotation = location.LocalRotation });
        }

        [Update]
        private void Update(Location location, in components.behaviour.Swing swing)
        {
            var phase = time.TotalElapsed * Speed;
            location.LocalRotation = swing.StartRotation * Quaternion.CreateFromYawPitchRoll(
                MathF.Sin(phase) * YRotation,
                MathF.Cos(phase) * XRotation,
                0f);
        }
    }
}
