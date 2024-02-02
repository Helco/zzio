using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using zzio;
using zzio.db;

namespace zzre.game.systems;

public partial class DialogGambling : ui.BaseScreen<components.DialogGambling, messages.DialogGambling>
{
    private static readonly components.ui.ElementId IDExit = new(1000);
    private static readonly components.ui.ElementId IDYes = new(1001);
    private static readonly components.ui.ElementId IDNo = new(1002);

    private static readonly UID UIDPurchaseItem = new(0x7B973CA1);
    private static readonly UID UIDItemProfile = new(0x2C2084B1);
    private static readonly UID UIDYouHave = new(0x070EE421);

    private readonly int currencyI = 23;
    private readonly int rows = 5;

    private readonly MappedDB db;
    private readonly IDisposable resetUISubscription;

    public DialogGambling(ITagContainer diContainer) : base(diContainer, BlockFlags.None)
    {
        db = diContainer.GetTag<MappedDB>();

        resetUISubscription = World.Subscribe<messages.DialogResetUI>(HandleResetUI);
        OnElementDown += HandleElementDown;
    }

    public override void Dispose()
    {
        base.Dispose();
        resetUISubscription.Dispose();
    }

    private void HandleResetUI(in messages.DialogResetUI message)
    {
        foreach (var entity in Set.GetEntities())
            entity.Dispose();
    }

    protected override void HandleOpen(in messages.DialogGambling message)
    {
        message.DialogEntity.Set(components.DialogState.Talk);

        World.Publish(new messages.DialogResetUI(message.DialogEntity));
        var uiEntity = World.CreateEntity();
        uiEntity.Set(new components.Parent(message.DialogEntity));

        uiEntity.Set(new components.DialogGambling{
            DialogEntity = message.DialogEntity,
            Currency = db.Items.ElementAt(currencyI),
            Cards = message.Cards,
            CardPurchaseButtons = new()
        });
        ref var gambling = ref uiEntity.Get<components.DialogGambling>();

        gambling.Profile = CreatePrimary(uiEntity, gambling);
    }
    private DefaultEcs.Entity CreatePrimary(DefaultEcs.Entity parent, components.DialogGambling gambling)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        preload.CreateDialogBackground(entity, animateOverlay: false, out var bgRect);
        CreateTopbar(entity, gambling.Currency);
        for (int i = 0; i < rows; i++) {
            Random rnd = new Random();
            var selectedCard = gambling.Cards.OrderBy(c => rnd.Next()).First();
            AddTrade(entity, gambling, selectedCard, i, bgRect);
        }

        CreateSingleButton(entity, new UID(0xF7DFDC21), IDExit, bgRect);

        return entity;
    }

    private DefaultEcs.Entity CreateItemProfile(DefaultEcs.Entity parent, components.DialogGambling gambling, SpellRow card)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        preload.CreateDialogBackground(entity, animateOverlay: false, out var bgRect);

        preload.CreateLabel(entity)
            .With(bgRect.Min + new Vector2(30, 22))
            .With(preload.Fnt001)
            .WithText(db.GetText(UIDItemProfile).Text)
            .Build();

        preload.CreateImage(entity)
            .With(bgRect.Center + new Vector2(-20, -100))
            .With(preload.Spl000, card.CardId.EntityId)
            .Build();

        preload.CreateLabel(entity)
            .With(bgRect.Center + new Vector2(-72, -40))
            .With(preload.Fnt001)
            .WithText(card.Name)
            .Build();

        preload.CreateLabel(entity)
            .With(bgRect.Min + new Vector2(50, 250))
            .With(preload.Fnt000)
            .WithText(card.Info)
            .WithLineHeight(14)
            .WithLineWrap(bgRect.Size.X - 100)
            .WithAnimation()
            .Build();

        preload.CreateLabel(entity)
            .With(new Vector2(bgRect.Center.X - 76, bgRect.Max.Y - 46))
            .With(preload.Fnt002)
            .WithText(db.GetText(UIDPurchaseItem).Text)
            .Build();

        preload.CreateButton(entity)
            .With(IDYes)
            .With(new Vector2(bgRect.Center.X + 20, bgRect.Max.Y - 65))
            .With(new components.ui.ButtonTiles(5, 6))
            .With(preload.Btn000)
            .Build();

        preload.CreateButton(entity)
            .With(IDNo)
            .With(new Vector2(bgRect.Center.X + 56, bgRect.Max.Y - 65))
            .With(new components.ui.ButtonTiles(7, 8))
            .With(preload.Btn000)
            .Build();

        return entity;
    }

    private void CreateTopbar(DefaultEcs.Entity parent, ItemRow currency)
    {
        var amountOwned = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>().CountCards(currency.CardId);

        preload.CreateLabel(parent)
            .With(new Vector2(-60, -170))
            .With(preload.Fnt000)
            .WithText($"{db.GetText(UIDYouHave).Text} {{{3000 + currency.CardId.EntityId}}}x{amountOwned}")
            .Build();
    }

    private void AddTrade(DefaultEcs.Entity entity, components.DialogGambling gambling, int? selectedCardId, int index, Rect bgRect)
    {
        var card = db.Spells.FirstOrDefault(c => c.CardId.EntityId == selectedCardId);

        var purchase = new components.ui.ElementId(index);
        var offset = bgRect.Center + new Vector2(-205, -120 + 55 * index);

        if (card == null) {
        preload.CreateLabel(entity)
            .With(offset + new Vector2(40, 16))
            .WithText("Blank")
            .With(preload.Fnt000)
            .Build();
            return;
        };

        preload.CreateImage(entity)
            .With(offset)
            .With(preload.Spl000, card.CardId.EntityId)
            .Build();

        preload.CreateLabel(entity)
            .With(offset + new Vector2(50, 16))
            .WithText(card.Name)
            .With(preload.Fnt002)
            .Build();

        preload.CreateButton(entity)
            .With(purchase)
            .With(offset + new Vector2(365, 0))
            .With(new components.ui.ButtonTiles(20, 21))
            .With(preload.Btn001)
            .Build();

        gambling.CardPurchaseButtons[purchase] = card;
    }

    private const float ButtonOffsetY = -50f;
    private void CreateSingleButton(DefaultEcs.Entity entity, UID textUID, components.ui.ElementId elementId, Rect bgRect)
    {
        preload.CreateButton(entity)
            .With(elementId)
            .With(new Vector2(bgRect.Center.X, bgRect.Max.Y + ButtonOffsetY))
            .With(new components.ui.ButtonTiles(0, 1))
            .With(components.ui.FullAlignment.TopCenter)
            .With(preload.Btn000)
            .WithLabel()
            .With(preload.Fnt000)
            .WithText(textUID)
            .Build();

        // TODO: Set cursor position in dialog gambling
    }
    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId clickedId)
    {
        var uiEntity = Set.GetEntities()[0];
        ref var gambling = ref uiEntity.Get<components.DialogGambling>();

        if (gambling.CardPurchaseButtons.TryGetValue(clickedId, out var card)) {
            gambling.Profile.Dispose();
            gambling.Purchase = card;
            gambling.Profile = CreateItemProfile(uiEntity, gambling, card);
        }
        else if (clickedId == IDYes) {
            var purchase = gambling.Purchase!;

            var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
            inventory.Add(purchase.CardId);

            gambling.Profile.Dispose();
            gambling.Profile = CreatePrimary(uiEntity, gambling);
        }
        else if (clickedId == IDNo) {
            gambling.Profile.Dispose();
            gambling.Purchase = null;
            gambling.Profile = CreatePrimary(uiEntity, gambling);
        }
        else if (clickedId == IDExit) {
            gambling.DialogEntity.Set(components.DialogState.NextScriptOp);
            uiEntity.Dispose();
        }
    }

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.DialogGambling component)
    {
    }
}
