using System;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using zzio.scn;

namespace zzre.game.systems
{
    public partial class NPCLookAtTrigger : AEntitySetSystem<float>
    {
        private readonly Scene scene;

        public NPCLookAtTrigger(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer2, useBuffer: true)
        {
            scene = diContainer.GetTag<Scene>();
        }

        private static DefaultEcs.EntitySet CreateEntityContainer2(object sender, DefaultEcs.World world) => world
            .GetEntities()
            .With<components.NPCLookAtTrigger>()
            .With<components.NPCState>(IsLookAtTriggerNPCState)
            .AsSet();

        private static bool IsLookAtTriggerNPCState(in components.NPCState value) =>
            value == components.NPCState.LookAtTrigger;

        protected override void Update(float state, in DefaultEcs.Entity entity) =>
            Update(state, entity,
                entity.Get<components.NPCType>(),
                entity.Get<Location>(),
                ref entity.Get<components.NPCLookAtTrigger>(),
                ref entity.Get<components.PuppetActorMovement>());

        private void Update(
            float elapsedTime,
            in DefaultEcs.Entity entity,
            components.NPCType npcType,
            Location location,
            ref components.NPCLookAtTrigger lookAt,
            ref components.PuppetActorMovement puppet)
        {
            lookAt.TimeLeft -= elapsedTime;
            if (lookAt.TimeLeft < 0f)
            {
                entity.Set(components.NPCState.Script);
                return;
            }

            var triggerIdx = lookAt.TriggerIdx;
            var trigger = scene.triggers.First(t => t.idx == triggerIdx);
            puppet.TargetDirection = Vector3.Normalize(trigger.pos.ToNumerics() - location.LocalPosition);
            
            // TODO: Add ActorHeadIK behavior for LookAtTrigger
        }
    }
}
