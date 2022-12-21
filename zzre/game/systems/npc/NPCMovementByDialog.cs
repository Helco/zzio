using System;
using DefaultEcs.System;
using DefaultEcs.Command;

namespace zzre.game.systems;

public partial class NPCMovementByDialog : NPCMovementBase
{
    private readonly EntityCommandRecorder recorder;

    public NPCMovementByDialog(ITagContainer diContainer) : base(diContainer, CreateEntityContainer, useBuffer: false)
    {
        recorder = diContainer.GetTag<EntityCommandRecorder>();
    }

    // the actual waypoint changing message is in NPCMovementByState

    [Update]
    private void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        components.NPCType npcType,
        Location location,
        ref components.NPCMovement move,
        ref components.NonFairyAnimation animation,
        in components.NPCIsWalkingByDialog byDialog)
    {
        if (!UpdateWalking(elapsedTime, entity, npcType, location, ref move))
            return;

        if (move.NextWaypointId == -1)
            animation.Next = zzio.AnimationType.Idle0;

        recorder.Record(entity).Remove<components.NPCIsWalkingByDialog>();
        recorder.Record(byDialog.DialogEntity).Set(components.DialogState.NextScriptOp);
    }
}
