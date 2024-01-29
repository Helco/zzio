﻿using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using zzio;
using zzio.db;

namespace zzre.game.systems;

public partial class DialogTrading : ui.BaseScreen<components.ui.DialogTrading, messages.DialogTrading>
{
    private static readonly components.ui.ElementId IDExit = new(1000);

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

        var trading = new components.ui.DialogTrading(message.DialogEntity, db.GetItem(message.CardUID), message.CardTrades, new());
        uiEntity.Set(trading);

        preload.CreateDialogBackground(uiEntity, animateOverlay: false, out var bgRect);

        trading.Primary = CreatePrimary(uiEntity, trading, bgRect);

    }

    private DefaultEcs.Entity CreatePrimary(DefaultEcs.Entity parent, components.ui.DialogTrading trading, Rect bgRect)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        CreateSingleButton(entity, new UID(0xF7DFDC21), IDExit, bgRect);
        CreateTopbar(entity, trading.Currency);
        var trades = trading.CardTrades.ToArray();
        for (int i = 0; i < trades.Length; i++)
            AddTrade(entity, trading, i, trades[i].Item1, trades[i].Item2, bgRect);
        return entity;
    }

    private DefaultEcs.Entity CreateItemProfile(DefaultEcs.Entity parent, ItemRow card)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        preload.CreateLabel(entity)
            .With(new Vector2(-220, -188))
            .With(preload.Fnt001)
            .WithText($"Item Profile")
            .Build();

        return entity;
    }

    private DefaultEcs.Entity CreateTopbar(DefaultEcs.Entity parent, ItemRow currency)
    {
        var cards = db.Items.OrderBy(itemRow => itemRow.CardId.EntityId).ToArray();
        var cardI = Array.FindIndex(cards, c => c.Name == currency.Name);

        var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
        var amountOwned = inventory.CountCards(currency.CardId);

        var entity = preload.CreateLabel(parent)
            .With(new Vector2(-60, -170))
            .With(preload.Fnt000)
            .WithText($"You have {{{3000 + cardI}}}x{amountOwned}")
            .Build();
        return entity;
    }

    private void AddTrade(DefaultEcs.Entity entity, components.ui.DialogTrading trading, int index, int price, UID uid, Rect bgRect)
    {
        var cards = db.Items.OrderBy(itemRow => itemRow.CardId.EntityId).ToArray();
        var card = db.GetItem(uid);

        var cardImage = new components.ui.ElementId(index);
        var purchase = new components.ui.ElementId(10 + index);

        var offset = bgRect.Center + new Vector2(-205, -120 + 55 * index);

        var button = preload.CreateButton(entity)
            .With(cardImage)
            .With(offset)
            .With(new components.ui.ButtonTiles(card.CardId.EntityId))
            .With(preload.Itm000)
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

        preload.CreateButton(entity)
            .With(purchase)
            .With(offset + new Vector2(365, 0))
            .With(new components.ui.ButtonTiles(20, 21))
            .With(preload.Btn001)
            .Build();

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
            CreateItemProfile(uiEntity, card);
        }
        if (clickedId == IDExit) {
            trading.DialogEntity.Set(components.DialogState.NextScriptOp);
            uiEntity.Dispose();
        }
    }

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.ui.DialogTrading component)
    {
    }
}
