namespace zzre.game.systems.ui;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using zzio;
using zzio.db;
using DefaultEcs;

public partial class ScrGotCard : BaseScreen<components.ui.ScrGotCard, messages.ui.OpenGotCard>
{
    private static readonly UID ExitButtonUID = new(0xF7DFDC21);
    private static readonly UID YouGotLabelUID = new(0xE846A2B1);
    private static readonly components.ui.ElementId IDExit = new(1);

    private readonly MappedDB db;

    public ScrGotCard(ITagContainer diContainer) : base(diContainer, BlockFlags.LockPlayerControl)
    {
        db = diContainer.GetTag<MappedDB>();
        OnElementDown += HandleElementDown;
    }

    protected override void HandleOpen(in messages.ui.OpenGotCard message)
    {
        CardId cardId;
        string cardName, cardInfo;
        switch(message.UID.Module) // TODO: Generalize Fairy/Item/Spell row into a MappedCardRow type
        {
            case (int)ModuleType.Fairy:
                var fairy = db.GetFairy(message.UID);
                (cardId, cardName, cardInfo) = (fairy.CardId, fairy.Name, fairy.Info);
                break;
            case (int)ModuleType.Item:
                var item = db.GetItem(message.UID);
                (cardId, cardName, cardInfo) = (item.CardId, item.Name, item.Info);
                break;
            case (int)ModuleType.Spell:
                var spell = db.GetSpell(message.UID);
                (cardId, cardName, cardInfo) = (spell.CardId, spell.Name, spell.Info);
                break;
            default: throw new ArgumentException($"UID is not referencing a card row: {message.UID}", nameof(message));
        }

        var entity = World.CreateEntity();
        entity.Set<components.ui.ScrGotCard>();

        preload.CreateDialogBackground(entity, animateOverlay: true, out var bgRect);
        preload.CreateButton(
            parent: entity,
            elementId: IDExit,
            new(bgRect.Center.X, bgRect.Max.Y - 50),
            text: db.GetText(ExitButtonUID).Text,
            new(0, 1),
            preload.Btn000,
            preload.Fnt000,
            out var buttonLabel,
            btnAlign: components.ui.FullAlignment.TopCenter);

        preload.CreateLabel(
            parent: entity,
            pos: bgRect.Min + new Vector2(50, 50),
            YouGotLabelUID,
            preload.Fnt001);

        preload.CreateImage(
            parent: entity,
            pos: new(0, bgRect.Min.Y + 110),
            preload.GetTileSheetByCardType(cardId.Type),
            tileI: cardId.EntityId,
            alignment: components.ui.FullAlignment.TopCenter);

        preload.CreateLabel(
            parent: entity,
            pos: new(0, bgRect.Min.Y + 170),
            text: message.Amount <= 1 
                ? cardName
                : $"\"{cardName}\" x {message.Amount}",
            preload.Fnt003,
            textAlign: components.ui.FullAlignment.TopCenter);

        preload.CreateAnimatedLabel(
            parent: entity,
            pos: bgRect.Min + new Vector2(50, 250),
            text: cardInfo,
            preload.Fnt000,
            lineHeight: 15,
            wrapLines: bgRect.Size.X - 100);

        // TODO: Fix broken joy animation on collecting item
        var game = ui.GetTag<Game>();
        game.Publish(new messages.SwitchAnimation(game.PlayerEntity, AnimationType.Joy));

        // TODO: Play sound and set cursor in ScrGotCard
    }

    private void HandleElementDown(Entity _, components.ui.ElementId elementId)
    {
        var screenEntity = Set.GetEntities()[0];
        if (elementId == IDExit)
            screenEntity.Dispose();
    }

    protected override void Update(float timeElapsed, in Entity entity, ref components.ui.ScrGotCard component)
    {
    }
}
