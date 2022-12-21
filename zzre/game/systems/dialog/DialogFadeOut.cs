using System;
using DefaultEcs.System;
using DefaultEcs.Command;

namespace zzre.game.systems;

public partial class DialogFadeOut : AEntitySetSystem<float>
{
    private EntityCommandRecorder recorder;
    private IDisposable setStateDisposable;

    public DialogFadeOut(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        recorder = diContainer.GetTag<EntityCommandRecorder>();
        setStateDisposable = World.SubscribeComponentChanged<components.DialogState>(HandleState);
    }

    public override void Dispose()
    {
        base.Dispose();
        setStateDisposable.Dispose();
    }

    private void HandleState(in DefaultEcs.Entity entity, in components.DialogState oldValue, in components.DialogState newValue)
    {
        if (newValue == components.DialogState.FadeOut)
        {
            World.Publish(new messages.DialogResetUI(entity));
            entity.Get<components.DialogCommonUI>().Letterbox.Set(components.ui.Fade.StdOut);
        }
    }

    [WithPredicate]
    private bool IsInFadeOut(in components.DialogState state) => state == components.DialogState.FadeOut;

    [Update]
    private void Update(in DefaultEcs.Entity entity, in components.DialogCommonUI commonUI)
    {
        if (!commonUI.Letterbox.Has<components.ui.Fade>())
            recorder.Record(entity).Dispose();
    }
}
