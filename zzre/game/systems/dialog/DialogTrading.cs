using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using zzio;
using zzio.db;

namespace zzre.game.systems;

public partial class DialogTrading : ui.BaseScreen<components.ui.DialogTrading, messages.DialogTrading>
{
    private static readonly components.ui.ElementId IDExit = new(1000);
    private static readonly components.ui.ElementId IDYes = new(1001);
    private static readonly components.ui.ElementId IDNo = new(1002);

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
        message.DialogEntity.Set(components.DialogState.Talk);

        World.Publish(new messages.DialogResetUI(message.DialogEntity));
        var uiEntity = World.CreateEntity();
        uiEntity.Set(new components.Parent(message.DialogEntity));

        uiEntity.Set(new components.ui.DialogTrading(
            message.DialogEntity,
            db.GetItem(message.CardUID),
            message.CardTrades,
            new()
        ));
        ref var trading = ref uiEntity.Get<components.ui.DialogTrading>();

        trading.Profile = CreatePrimary(uiEntity, trading);
    }

    private DefaultEcs.Entity CreatePrimary(DefaultEcs.Entity parent, components.ui.DialogTrading trading)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        preload.CreateDialogBackground(entity, animateOverlay: false, out var bgRect);
        CreateTopbar(entity, trading.Currency);
        var trades = trading.CardTrades.ToArray();
        for (int i = 0; i < trades.Length; i++)
            AddTrade(entity, trading, i, trading.Currency, trades[i].Item1, trades[i].Item2, bgRect);
        CreateSingleButton(entity, new UID(0xF7DFDC21), IDExit, bgRect);

        return entity;
    }

    private DefaultEcs.Entity CreateItemProfile(DefaultEcs.Entity parent, components.ui.DialogTrading trading, ItemRow card)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        preload.CreateDialogBackground(entity, animateOverlay: false, out var bgRect);
        preload.CreateLabel(entity)
            .With(new Vector2(-220, -188))
            .With(preload.Fnt001)
            .WithText($"Item Profile")
            .Build();

        preload.CreateImage(entity)
            .With(new Vector2(-20, -100))
            .With(preload.Itm000, card.CardId.EntityId)
            .Build();

        preload.CreateLabel(entity)
            .With(new Vector2(-20-52, -40))
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
            .With(new Vector2(-76, 164))
            .With(preload.Fnt002)
            .WithText("Purchase item?")
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
        var cards = db.Items.OrderBy(itemRow => itemRow.CardId.EntityId).ToArray();
        var cardI = Array.FindIndex(cards, c => c.Name == currency.Name);

        var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
        var amountOwned = inventory.CountCards(currency.CardId);

        preload.CreateLabel(parent)
            .With(new Vector2(-60, -170))
            .With(preload.Fnt000)
            .WithText($"You have {{{3000 + cardI}}}x{amountOwned}")
            .Build();
    }

    private void AddTrade(
        DefaultEcs.Entity entity,
        components.ui.DialogTrading trading,
        int index,
        ItemRow currency,
        int price,
        UID uid,
        Rect bgRect)
    {
        var cards = db.Items.OrderBy(itemRow => itemRow.CardId.EntityId).ToArray();
        var card = db.GetItem(uid);

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
        if (inventory.CountCards(currency.CardId) >= price) {
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

        // TODO: Set cursor position in dialog trading
    }
    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId clickedId)
    {
        var uiEntity = Set.GetEntities()[0];
        ref var trading = ref uiEntity.Get<components.ui.DialogTrading>();

        if (trading.CardPurchaseButtons.TryGetValue(clickedId, out var card)) {
            trading.Profile.Dispose();
            trading.Purchase = card;
            trading.Profile = CreateItemProfile(uiEntity, trading, card);
        }
        else if (clickedId == IDYes) {
            var purchase = trading.Purchase!;
            var price = trading.CardTrades.First(((int price, UID uid) trade) => db.GetItem(trade.uid).Name == purchase.Name).Item1;

            var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
            inventory.Add(purchase.CardId);
            inventory.RemoveCards(trading.Currency.CardId, (uint)price);

            trading.Profile.Dispose();
            trading.Profile = CreatePrimary(uiEntity, trading);
        }
        else if (clickedId == IDNo) {
            trading.Profile.Dispose();
            trading.Purchase = default;
            trading.Profile = CreatePrimary(uiEntity, trading);
        }
        else if (clickedId == IDExit) {
            trading.DialogEntity.Set(components.DialogState.NextScriptOp);
            uiEntity.Dispose();
        }
    }

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.ui.DialogTrading component)
    {
    }
}
