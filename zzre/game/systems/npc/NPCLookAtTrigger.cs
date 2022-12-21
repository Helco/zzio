using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using zzio.scn;

namespace zzre.game.systems;

[PauseDuring(PauseTrigger.UIScreen)]
public partial class NPCLookAtTrigger : AEntitySetSystem<float>
{
    public NPCLookAtTrigger(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
    }

    [WithPredicate]
    private static bool IsLookAtTriggerNPCState(in components.NPCState value) =>
        value == components.NPCState.LookAtTrigger;

    [Update]
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

        if (lookAt.Trigger == null)
        {
            var triggerIdx = lookAt.TriggerIdx;
            lookAt.Trigger = World.GetEntities()
                .With((in Trigger t) => t.idx == triggerIdx)
                .AsEnumerable()
                .First()
                .Get<Trigger>();
        }
        puppet.TargetDirection = Vector3.Normalize(lookAt.Trigger.pos - location.LocalPosition);

        // TODO: Add ActorHeadIK behavior for LookAtTrigger
    }
}
