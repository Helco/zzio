using System;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.db;

namespace zzre.game.systems;

public partial class DialogTrading : ui.BaseScreen<components.DialogTrading, messages.DialogTrading>
{
    private static readonly components.ui.ElementId IDExit = new(1000);
    private static readonly components.ui.ElementId IDYes = new(1001);
    private static readonly components.ui.ElementId IDNo = new(1002);

    private static readonly UID UIDPurchaseItem = new(0x7B973CA1);
    private static readonly UID UIDItemProfile = new(0x2C2084B1);

    private readonly MappedDB db;
    private readonly IDisposable resetUISubscription;

    public DialogTrading(ITagContainer diContainer) : base(diContainer, BlockFlags.None)
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

    protected override void HandleOpen(in messages.DialogTrading message)
    {
        message.DialogEntity.Set(components.DialogState.Trading);

        World.Publish(new messages.DialogResetUI(message.DialogEntity));
        var uiEntity = World.CreateEntity();
        uiEntity.Set(new components.Parent(message.DialogEntity));

        uiEntity.Set(new components.DialogTrading{
            DialogEntity = message.DialogEntity,
            Currency = db.GetItem(message.CurrencyUID),
            CardTrades = message.CardTrades,
            CardPurchaseButtons = []
        });
        ref var trading = ref uiEntity.Get<components.DialogTrading>();

        trading.Profile = CreatePrimary(uiEntity, trading);
    }

    private DefaultEcs.Entity CreatePrimary(DefaultEcs.Entity parent, components.DialogTrading trading)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        preload.CreateDialogBackground(entity, animateOverlay: false, out var bgRect);
        preload.CreateCurrencyLabel(entity, trading.Currency, zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>());
        for (int i = 0; i < trading.CardTrades.Count; i++)
            AddTrade(entity, trading, i, bgRect);
        preload.CreateSingleDialogButton(entity, new UID(0xF7DFDC21), IDExit, bgRect);

        return entity;
    }

    private DefaultEcs.Entity CreateItemProfile(DefaultEcs.Entity parent, components.DialogTrading trading, ItemRow card)
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
            .With(preload.Itm000, card.CardId.EntityId)
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

    private void AddTrade(DefaultEcs.Entity entity, components.DialogTrading trading, int index, Rect bgRect)
    {
        var price = trading.CardTrades[index].price;
        var card = db.GetItem(trading.CardTrades[index].uid);

        var purchase = new components.ui.ElementId(index);
        var offset = bgRect.Center + new Vector2(-205, -120 + 55 * index);

        preload.CreateImage(entity)
            .With(offset)
            .With(preload.Itm000, card.CardId.EntityId)
            .Build();

        preload.CreateLabel(entity)
            .With(offset + new Vector2(50, 16))
            .WithText(card.Name)
            .With(preload.Fnt002)
            .Build();

        preload.CreateLabel(entity)
            .With(offset + new Vector2(325, 12))
            .WithText($"{{0*x}}{price}")
            .With(preload.Fnt000)
            .Build();

        var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
        if (inventory.CountCards(trading.Currency.CardId) >= price) {
            preload.CreateButton(entity)
                .With(purchase)
                .With(offset + new Vector2(365, 0))
                .With(new components.ui.ButtonTiles(20, 21))
                .With(preload.Btn001)
                .Build();
        } else {
            preload.CreateImage(entity)
                .With(offset + new Vector2(365, 0))
                .With(preload.Btn001, 22)
                .Build();
        }

        trading.CardPurchaseButtons[purchase] = card;
    }

    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId clickedId)
    {
        var uiEntity = Set.GetEntities()[0];
        ref var trading = ref uiEntity.Get<components.DialogTrading>();

        if (trading.CardPurchaseButtons.TryGetValue(clickedId, out var card)) {
            trading.Profile.Dispose();
            trading.Purchase = card;
            trading.Profile = CreateItemProfile(uiEntity, trading, card);
        }
        else if (clickedId == IDYes) {
            World.Publish(new messages.SpawnSample($"resources/audio/sfx/gui/_g008.wav"));

            var purchase = trading.Purchase!;
            var price = trading.CardTrades.First(trade => trade.uid == purchase.Uid).price;

            var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
            inventory.Add(purchase.CardId);
            inventory.RemoveCards(trading.Currency.CardId, (uint)price);

            trading.Profile.Dispose();
            trading.Profile = CreatePrimary(uiEntity, trading);
        }
        else if (clickedId == IDNo) {
            World.Publish(new messages.SpawnSample($"resources/audio/sfx/gui/_g009.wav"));

            trading.Profile.Dispose();
            trading.Purchase = null;
            trading.Profile = CreatePrimary(uiEntity, trading);
        }
        else if (clickedId == IDExit) {
            trading.DialogEntity.Set(components.DialogState.NextScriptOp);
            uiEntity.Dispose();
        }
    }

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.DialogTrading component)
    {
    }
}
