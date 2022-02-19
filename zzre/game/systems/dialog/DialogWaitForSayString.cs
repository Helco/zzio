using System;
using System.Numerics;
using DefaultEcs.System;
using Veldrid;

namespace zzre.game.systems
{
    public partial class DialogWaitForSayString : AEntitySetSystem<float>
    {
        private const MouseButton SpeedUpButton = Veldrid.MouseButton.Left;
        private const int SlowSegmentsToAdd = 4;
        private const int FastSegmentsToAdd = 16;

        private readonly IZanzarahContainer zzContainer;
        private bool didClick = false;

        public DialogWaitForSayString(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
        {
            zzContainer = diContainer.GetTag<IZanzarahContainer>();
            zzContainer.OnMouseDown += HandleMouseDown;
        }

        public override void Dispose()
        {
            base.Dispose();
            zzContainer.OnMouseDown -= HandleMouseDown;
        }

        private void HandleMouseDown(MouseButton button, Vector2 _)
        {
            if (button == SpeedUpButton)
                didClick = true;
        }

        [WithPredicate]
        private bool IsInWaitForSayString(in components.DialogState state) => state == components.DialogState.WaitForSayString;

        [Update]
        private void Update(in DefaultEcs.Entity entity, in components.DialogCommonUI commonUI)
        {
            var didClick = this.didClick;
            this.didClick = false;

            if (commonUI.SayLabel.IsAlive)
            {
                ref var sayAnimation = ref commonUI.SayLabel.Get<components.ui.AnimatedLabel>();
                sayAnimation = sayAnimation with
                {
                    SegmentsPerAdd = didClick ? FastSegmentsToAdd : SlowSegmentsToAdd
                }; // yes the fast segment add is a frame-perfect input

                if (!sayAnimation.IsDone)
                    return;
            }

            World.Publish(default(messages.DialogSayStringFinished));

            // but what if nobody reacted to the message? - then we are click to continue
            if (didClick && entity.Get<components.DialogState>() == components.DialogState.WaitForSayString)
                entity.Set(components.DialogState.NextScriptOp);
        }
    }
}
