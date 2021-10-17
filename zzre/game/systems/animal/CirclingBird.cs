using System;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.System;

namespace zzre.game.systems
{
    public partial class CirclingBird : AEntitySetSystem<float>
    {
        private readonly IDisposable addSubscription;

        public CirclingBird(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, null, 0)
        {
            addSubscription = World.SubscribeComponentAdded<components.CirclingBird>(HandleAddedComponent);
        }

        public override void Dispose()
        {
            base.Dispose();
            addSubscription.Dispose();
        }

        private void HandleAddedComponent(in Entity entity, in components.CirclingBird value)
        {
            var location = entity.Get<Location>();
            var trigger = entity.Get<zzio.scn.Trigger>();
            entity.Set(new components.CirclingBird()
            {
                Center = location.LocalPosition + location.GlobalForward * (trigger.ii2 * 0.01f),
                Speed = unchecked((int)trigger.ii3) * 0.001f
            });
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
}
