using System;
using System.Numerics;
using DefaultEcs.System;

namespace zzre.game.systems;

public partial class DialogDelay : AEntitySetSystem<float>
{
    private readonly Game game;

    public DialogDelay(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        game = diContainer.GetTag<Game>();
    }

    [WithPredicate]
    private bool IsInDelayState(in components.DialogState state) => state == components.DialogState.Delay;

    [Update]
    private static void Update(
        float timeElapsed,
        in DefaultEcs.Entity dialogEntity,
        ref components.DialogDelay delay)
    {
        var newTimeLeft = Math.Max(0f, delay.TimeLeft - timeElapsed);
        if (newTimeLeft == 0f)
            dialogEntity.Set(components.DialogState.NextScriptOp);
        else
            delay.TimeLeft = newTimeLeft;
    }
}
