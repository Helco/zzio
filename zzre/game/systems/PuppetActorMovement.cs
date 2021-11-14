using System;
using System.Numerics;
using DefaultEcs.System;

namespace zzre.game.systems
{
    public partial class PuppetActorMovement : AEntitySetSystem<float>
    {
        private const float Curvature = 100f;
        private const float SlerpSpeed = 2f;

        public PuppetActorMovement(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, null, 0)
        {
            World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
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
            var newForward = MathEx.HorizontalSlerp(actorLocation.InnerForward, targetDir, Curvature, elapsedTime * SlerpSpeed);
            actorLocation.LookIn(newForward);
        }
    }
}
