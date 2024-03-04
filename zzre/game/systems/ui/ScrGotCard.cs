using System;
using System.Numerics;
using zzio;
using zzio.db;

namespace zzre.game.systems.ui;

public partial class ScrGotCard : BaseScreen<components.ui.ScrGotCard, messages.ui.OpenGotCard>
{
    private static readonly UID ExitButtonUID = new(0xF7DFDC21);
    private static readonly UID YouGotLabelUID = new(0xE846A2B1);
    private static readonly components.ui.ElementId IDExit = new(1);

    private readonly MappedDB db;

    public ScrGotCard(ITagContainer diContainer) : base(diContainer, BlockFlags.LockPlayerControl | BlockFlags.NotifyGameScreen)
    {
        db = diContainer.GetTag<MappedDB>();
        OnElementDown += HandleElementDown;
    }

    protected override void HandleOpen(in messages.ui.OpenGotCard message)
    {
        CardId cardId;
        string cardName, cardInfo;
        switch (message.UID.Module) // TODO: Generalize Fairy/Item/Spell row into a MappedCardRow type
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

        preload.CreateButton(entity)
            .With(IDExit)
            .With(new Vector2(bgRect.Center.X, bgRect.Max.Y - 50))
            .With(new components.ui.ButtonTiles(0, 1))
            .With(components.ui.FullAlignment.TopCenter)
            .With(UIPreloadAsset.Btn000)
            .WithLabel()
            .With(UIPreloadAsset.Fnt000)
            .WithText(db.GetText(ExitButtonUID).Text)
            .Build();

        preload.CreateLabel(entity)
            .With(bgRect.Min + new Vector2(50, 50))
            .With(UIPreloadAsset.Fnt001)
            .WithText(YouGotLabelUID)
            .Build();

        preload.CreateImage(entity)
            .With(new Vector2(0, bgRect.Min.Y + 110))
            .With(cardId)
            .With(components.ui.FullAlignment.TopCenter)
            .Build();

        preload.CreateLabel(entity)
            .With(new Vector2(0, bgRect.Min.Y + 170))
            .With(UIPreloadAsset.Fnt003)
            .With(components.ui.FullAlignment.TopCenter)
            .WithText(message.Amount <= 1
                ? cardName
                : $"\"{cardName}\" x {message.Amount}")
            .Build();

        preload.CreateLabel(entity)
            .With(bgRect.Min + new Vector2(50, 250))
            .With(UIPreloadAsset.Fnt000)
            .WithText(cardInfo)
            .WithLineHeight(15)
            .WithLineWrap(bgRect.Size.X - 100)
            .WithAnimation()
            .Build();

        var game = ui.GetTag<Game>();
        game.Publish(new messages.SwitchAnimation(game.PlayerEntity, AnimationType.Joy));

        var sampleEntity = ui.World.CreateEntity(); // create own entity otherwise the finishing sound will close the screen
        game.Publish(new messages.SpawnSample(
            "resources/audio/sfx/specials/_s001.wav",
            AsEntity: sampleEntity));
        sampleEntity.Set(new components.Parent(entity));

        // TODO: Set cursor in ScrGotCard
    }

    private void HandleElementDown(DefaultEcs.Entity _, components.ui.ElementId elementId)
    {
        var screenEntity = Set.GetEntities()[0];
        if (elementId == IDExit) {
            World.Publish(new messages.SpawnSample($"resources/audio/sfx/gui/_g003.wav"));
            screenEntity.Dispose();
        }

    }

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.ui.ScrGotCard component)
    {
    }
}
