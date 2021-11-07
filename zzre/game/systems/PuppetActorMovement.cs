using System;
using System.Numerics;
using DefaultEcs;
using DefaultEcs.System;

namespace zzre.game.systems
{
    public partial class PuppetActorMovement : AEntitySetSystem<float>
    {
        private const float Curvature = 100f;
        private const float SlerpSpeed = 2f;

        private readonly IDisposable addSubscription;

        public PuppetActorMovement(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, null, 0)
        {
            addSubscription = World.SubscribeComponentAdded<components.PuppetActorMovement>(HandleAddedComponent);
            World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        }

        public override void Dispose()
        {
            base.Dispose();
            addSubscription.Dispose();
        }

        private void HandleAddedComponent(in Entity entity, in components.PuppetActorMovement value)
        {
            var parent = entity.Get<components.ActorPart>().ParentActor;
            var myLocation = entity.Get<Location>();
            var parentLocation = parent.Get<Location>();
            myLocation.Parent = parentLocation.Parent; // to prevent parent moving this actor
        }

        private void HandleSceneLoaded(in messages.SceneLoaded message)
        {
            foreach (var entity in Set.GetEntities())
            {
                var parent = entity.Get<components.ActorPart>().ParentActor;
                var myLocation = entity.Get<Location>();
                var parentLocation = parent.Get<Location>();
                myLocation.LocalPosition = parentLocation.LocalPosition;
                myLocation.LocalRotation = parentLocation.LocalRotation;
                entity.Get<components.PuppetActorMovement>().TargetDirection = myLocation.InnerForward;
            }
        }

        [Update]
        private static void Update(float elapsedTime,
            in components.PuppetActorMovement movement,
            in components.ActorPart actorPart,
            in Location actorLocation)
        {
            var parentLocation = actorPart.ParentActor.Get<Location>();
            actorLocation.LocalPosition = parentLocation.LocalPosition; // definitly no interpolation for position

            // again weirdness from original engine
            var targetDir = movement.TargetDirection;
            targetDir.Y = parentLocation.InnerForward.Y;
            targetDir = Vector3.Normalize(targetDir);
            var newForward = HorizontalSlerp(actorLocation.InnerForward, targetDir, Curvature, elapsedTime * SlerpSpeed);
            actorLocation.LookIn(newForward);
        }

        private static Vector3 HorizontalSlerp(Vector3 from, Vector3 to, float curvature, float time)
        {
            var fromAngle = MathF.Atan2(from.X, from.Z);
            var angleDelta = MathF.Atan2(to.X, to.Z) - fromAngle;
            if (angleDelta < -MathF.PI)
                angleDelta += 2 * MathF.PI;
            if (angleDelta > MathF.PI)
                angleDelta -= 2 * MathF.PI;
            var newAngle = (1f - 1f / MathF.Pow(curvature, time)) * angleDelta + fromAngle;

            return new Vector3(
                MathF.Sin(newAngle),
                to.Y,
                MathF.Cos(newAngle));
        }
    }
}
