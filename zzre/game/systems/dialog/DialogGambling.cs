using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using zzio;
using zzio.db;

namespace zzre.game.systems;

public partial class DialogGambling : ui.BaseScreen<components.DialogGambling, messages.DialogGambling>
{
    private static readonly components.ui.ElementId IDExit = new(1000);
    private static readonly components.ui.ElementId IDRepeat = new(1001);
    private static readonly components.ui.ElementId IDYes = new(1002);
    private static readonly components.ui.ElementId IDNo = new(1003);

    private static readonly UID UIDSpellProfile = new(0xBFC6DD81);
    private static readonly UID UIDTakeIt = new(0x84D35581);
    private static readonly UID UIDANewSpell = new(0xC38FEBB1);
    private static readonly UID UIDOffensiveSpell = new(0xD4B02981);
    private static readonly UID UIDPassiveSpell = new(0x515E2981);

    private readonly int currencyI = 23;
    private readonly int rows = 5;
    private readonly float rowAnimationDelay = 1f;

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
            SelectedCards = new(),
            CardPurchaseButtons = new()
        });
        ref var gambling = ref uiEntity.Get<components.DialogGambling>();

        preload.CreateDialogBackground(uiEntity, animateOverlay: false, out var bgRect);
        gambling.bgRect = bgRect;

        gambling.Profile = CreatePrimary(uiEntity, ref gambling);
    }
    private DefaultEcs.Entity CreatePrimary(DefaultEcs.Entity parent, ref components.DialogGambling gambling)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        preload.CreateCurrencyLabel(entity, gambling.Currency, zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>());
        // for (int i = 0; i < rows; i++) {
        //     Random rnd = new();
        //     var selectedCard = gambling.Cards.MinBy(c => rnd.Next());
        //     AddTrade(entity, gambling, selectedCard, i);
        // }
        gambling.RowAnimationTimeLeft = rowAnimationDelay;

        // preload.CreateSingleButton(entity, new UID(0xF7DFDC21), IDExit, bgRect);
        // preload.CreateSingleButton(entity, new UID(0x91A7E821), IDRepeat, bgRect, offset: 1);

        return entity;
    }

    private DefaultEcs.Entity CreateSpellProfile(DefaultEcs.Entity parent, ref components.DialogGambling gambling, SpellRow card)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        preload.CreateLabel(entity)
            .With(gambling.bgRect.Min + new Vector2(30, 22))
            .With(preload.Fnt001)
            .WithText(db.GetText(UIDSpellProfile).Text)
            .Build();

        preload.CreateImage(entity)
            .With(gambling.bgRect.Min + new Vector2(50+40, 50+26))
            .With(preload.Spl000, card.CardId.EntityId)
            .Build();

        preload.CreateLabel(entity)
            .With(gambling.bgRect.Min + new Vector2(70+40+30, 50+33))
            .With(preload.Fnt003)
            .WithText(card.Name)
            .Build();

        var texts = new (int row, int col, string text)[] {
            (0, 0, db.GetText(UIDANewSpell).Text.ToUpper(new CultureInfo("en-US", false))),
            (1, 0, "Offensive Spell - Nature"),
            (2, 0, "Mana"),
            (2, 1, "{104}" + (card.Mana == 5 ? "-/-" : $"{card.MaxMana}/{card.MaxMana}")),
            (3, 0, "Level"),
            (3, 1, preload.GetSpellPrices(card)),
            (4, 0, "Damage"),
            (4, 1, preload.GetLightsIndicator(card.Damage)),
            (5, 0, "Fire Rate"),
            (5, 1, preload.GetLightsIndicator(card.Loadup))
        };
        for (int i = 0; i < texts.Length; i++)
            preload.CreateLabel(entity)
                .With(gambling.bgRect.Min + new Vector2(50+40 + texts[i].col * 90, 100+36 + texts[i].row * 28))
                .With(preload.Fnt002)
                .WithText(texts[i].text)
                .Build();

        preload.CreateLabel(entity)
            .With(new Vector2(gambling.bgRect.Center.X - 76-59, gambling.bgRect.Max.Y - 46))
            .With(preload.Fnt002)
            .WithText(db.GetText(UIDTakeIt).Text)
            .Build();

        preload.CreateButton(entity)
            .With(IDYes)
            .With(new Vector2(gambling.bgRect.Center.X + 20-70, gambling.bgRect.Max.Y - 65+5))
            .With(new components.ui.ButtonTiles(5, 6))
            .With(preload.Btn000)
            .Build();

        preload.CreateButton(entity)
            .With(IDNo)
            .With(new Vector2(gambling.bgRect.Center.X + 56-56, gambling.bgRect.Max.Y - 65+5))
            .With(new components.ui.ButtonTiles(7, 8))
            .With(preload.Btn000)
            .Build();

        return entity;
    }

    private void AddRandomTrade(DefaultEcs.Entity entity, ref components.DialogGambling gambling) {
        Random rnd = new();
        var selectedCard = gambling.Cards.MinBy(c => rnd.Next());
        gambling.SelectedCards.Add(db.Spells.FirstOrDefault(c => c.CardId.EntityId == selectedCard));
        AddTrade(entity, ref gambling, selectedCard, gambling.SelectedCards.Count-1);
    }

    private void AddTrade(DefaultEcs.Entity entity, ref components.DialogGambling gambling, int? selectedCardId, int index)
    {
        var card = db.Spells.FirstOrDefault(c => c.CardId.EntityId == selectedCardId);

        var purchase = new components.ui.ElementId(index);
        var offset = gambling.bgRect.Center + new Vector2(25-205, 20-30-120 + 50 * index);

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
            .With(offset + new Vector2(49, 16-9))
            .WithLineHeight(13)
            .WithText(GetSpellLabel(card))
            .With(preload.Fnt002)
            .Build();

        preload.CreateButton(entity)
            .With(purchase)
            .With(offset + new Vector2(370, -1))
            .With(new components.ui.ButtonTiles(20, 21))
            .With(preload.Btn001)
            .Build();

        gambling.CardPurchaseButtons[purchase] = card;
    }

    private string GetSpellLabel(SpellRow card) {
        var name = card.Name;
        var type = card.Type == 0 ? "Active Spell" : "Passive Spell";
        var className = preload.GetClassText(card.PriceA);
        var prices = preload.GetSpellPrices(card);
        return $"{name}\n{type} - {className} - {prices}";
    }

    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId clickedId)
    {
        var uiEntity = Set.GetEntities()[0];
        ref var gambling = ref uiEntity.Get<components.DialogGambling>();

        if (gambling.CardPurchaseButtons.TryGetValue(clickedId, out var card)) {
            gambling.Profile.Dispose();
            gambling.Purchase = card;
            gambling.Profile = CreateSpellProfile(uiEntity, ref gambling, card);
        }
        else if (clickedId == IDYes) {
            zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>().Add(gambling.Purchase!.CardId);
            gambling.Profile.Dispose();
            gambling.Profile = CreatePrimary(uiEntity, ref gambling);
        }
        else if (clickedId == IDNo) {
            gambling.Profile.Dispose();
            gambling.Profile = CreatePrimary(uiEntity, ref gambling);
        }
        else if (clickedId == IDRepeat) {
            gambling.Profile.Dispose();
            gambling.Profile = CreatePrimary(uiEntity, ref gambling);
        }
        else if (clickedId == IDExit) {
            gambling.DialogEntity.Set(components.DialogState.NextScriptOp);
            uiEntity.Dispose();
        }
    }

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.DialogGambling gambling)
    {
        if (gambling.RowAnimationTimeLeft != null) {
            gambling.RowAnimationTimeLeft -= timeElapsed;
            if (gambling.RowAnimationTimeLeft <= 0f) {
                AddRandomTrade(entity, ref gambling);
                gambling.RowAnimationTimeLeft = rowAnimationDelay;
            }
        }
    }
}
