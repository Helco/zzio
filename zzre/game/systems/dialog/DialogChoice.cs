using System;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.db;

namespace zzre.game.systems
{
    public partial class DialogChoice : ui.BaseScreen<components.DialogChoice, messages.DialogSayStringFinished>
    {
        private static readonly components.ui.ElementId IDFirstChoice = new(10);
        private static readonly components.ui.ElementId IDLastChoice = new(20);

        private readonly IDisposable resetUISubscription;
        private readonly IDisposable addChoiceSubscription;

        public DialogChoice(ITagContainer diContainer) : base(diContainer, BlockFlags.None)
        {
            resetUISubscription = World.Subscribe<messages.DialogResetUI>(HandleResetUI);
            addChoiceSubscription = World.Subscribe<messages.DialogAddChoice>(HandleAddChoice);
            OnElementDown += HandleElementDown;
        }

        public override void Dispose()
        {
            base.Dispose();
            resetUISubscription.Dispose();
            addChoiceSubscription.Dispose();
        }

        private void HandleResetUI(in messages.DialogResetUI message)
        {
            foreach (var entity in Set.GetEntities())
                entity.Dispose();
        }

        private void HandleAddChoice(in messages.DialogAddChoice message)
        {
            DefaultEcs.Entity uiEntity;
            if (Set.Count == 0)
            {
                uiEntity = World.CreateEntity();
                uiEntity.Set(new components.Parent(message.DialogEntity));
                uiEntity.Set(new components.DialogChoice(message.DialogEntity, Array.Empty<int>()));
            }
            else
                uiEntity = Set.GetEntities()[0];

            ref var dialogChoices = ref uiEntity.Get<components.DialogChoice>();
            dialogChoices = dialogChoices with { Labels = dialogChoices.Labels.Append(message.Label).ToArray() };

            var sayLabel = message.DialogEntity.Get<components.DialogCommonUI>().SayLabel;
            var sayLabelPosY = sayLabel.Get<Rect>().Min.Y;
            var sayLabelHeight = sayLabel.Get<rendering.TileSheet>().GetTextHeight(sayLabel.Get<components.ui.Label>().Text);
            var buttonI = dialogChoices.Labels.Length - 1;
            var buttonEntity = preload.CreateImageButton(
                parent: uiEntity,
                IDFirstChoice + buttonI,
                new Vector2(40, sayLabelPosY + sayLabelHeight + 23 + 18 * buttonI),
                new(4, 3),
                preload.Fsp000,
                offset: components.ui.UIOffset.ScreenUpperLeft);
            ref var buttonRect = ref buttonEntity.Get<Rect>();

            var labelEntity = preload.CreateLabel(
                parent: uiEntity,
                new(buttonRect.Max.X + 10f, buttonRect.Center.Y),
                message.UID,
                preload.Fnt003,
                offset: components.ui.UIOffset.ScreenUpperLeft);
            ref var labelRect = ref labelEntity.Get<Rect>();
            var labelTileSheet = labelEntity.Get<rendering.TileSheet>();
            var labelWidth = labelTileSheet.GetUnformattedWidth(labelEntity.Get<components.ui.Label>().Text);
            
            labelRect = new Rect(labelRect.Center - Vector2.UnitY * labelTileSheet.GetPixelSize(0).Y / 2, labelRect.Size);
            buttonRect = ref buttonEntity.Get<Rect>(); // creating the label could have invalidated the reference
            buttonRect = Rect.FromTopLeftSize(buttonRect.Min, buttonRect.Size with { X = buttonRect.Size.X + labelWidth + 20 });

            // TODO: Set cursor position to first choice button
        }

        protected override void HandleOpen(in messages.DialogSayStringFinished message)
        {
            if (Set.Count == 0)
                return;
            message.DialogEntity.Set(components.DialogState.Choice);
        }

        private void HandleElementDown(DefaultEcs.Entity _, components.ui.ElementId clickedId)
        {
            if (!clickedId.InRange(IDFirstChoice, IDLastChoice, out var choiceI))
                return;

            var uiEntity = Set.GetEntities()[0];
            var dialogChoice = uiEntity.Get<components.DialogChoice>();
            var dialogEntity = dialogChoice.DialogEntity;
            ref var script = ref dialogEntity.Get<components.ScriptExecution>();
            script.CurrentI = script.LabelTargets[dialogChoice.Labels[choiceI]];
            dialogEntity.Set(components.DialogState.NextScriptOp);
            uiEntity.Dispose();
        }

        protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.DialogChoice component)
        {
        }
    }
}
