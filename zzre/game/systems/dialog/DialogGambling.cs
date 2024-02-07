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
    private static readonly UID UIDNewSpell = new(0xC38FEBB1);
    private static readonly UID UIDOldSpell = new(0x92A1EBB1);
    private static readonly UID UIDOldSpell1 = new(0xE9C9EFB1);
    private static readonly UID UIDOldSpell2 = new(0x8B19EFB1);
    private static readonly UID UIDOffensiveSpell = new(0xD4B02981);
    private static readonly UID UIDPassiveSpell = new(0x515E2981);
    private static readonly UID UIDBlank = new(0x1AF6CAB1);

    private static readonly UID UIDMana = new(0x238A3981);
    private static readonly UID UIDLevel = new(0xCDF3D81);
    private static readonly UID UIDDamage = new(0xE946ECA1);
    private static readonly UID UIDLoadup = new(0x4211121);

    private readonly int currencyI = 23;
    private readonly int cloverleafI = 64;
    private readonly int rows = 5;
    private readonly int pricePerRow = 2;
    private readonly float rowAnimationDelay = 0.5f;

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
            Cards = CloverleafFilter(message.Cards),
            SelectedCards = new(),
            CardPurchaseButtons = new()
        });
        ref var gambling = ref uiEntity.Get<components.DialogGambling>();

        preload.CreateDialogBackground(uiEntity, animateOverlay: false, out var bgRect);
        gambling.bgRect = bgRect;

        gambling.Profile = CreatePrimary(uiEntity, ref gambling);
    }

    private DefaultEcs.Entity CreatePrimary(DefaultEcs.Entity parent, ref components.DialogGambling gambling, bool allowPurchaseButtons = true)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        gambling.CurrencyLabel = preload.CreateCurrencyLabel(entity, gambling.Currency, zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>());
        if (gambling.SelectedCards.Count == rows) {
            RebuildPrimary(entity, ref gambling, allowPurchaseButtons);
        } else {
            StartAnimation(ref gambling);
        }

        return entity;
    }

    private void RebuildPrimary(DefaultEcs.Entity parent, ref components.DialogGambling gambling, bool allowPurchaseButtons)
    {
        for (int i = 0; i < rows; i++)
            AddTrade(parent, ref gambling, i);
        AddBottomButtons(parent, ref gambling);
        if (allowPurchaseButtons)
            AddPurchaseButtons(parent, ref gambling);
    }

    private void AddBottomButtons(DefaultEcs.Entity parent, ref components.DialogGambling gambling)
    {
        if (CanAfford(ref gambling))
            preload.CreateSingleButton(parent, new UID(0x91A7E821), IDRepeat, gambling.bgRect, offset: 1);
        preload.CreateSingleButton(parent, new UID(0xF7DFDC21), IDExit, gambling.bgRect);
    }

    private void AddPurchaseButtons(DefaultEcs.Entity parent, ref components.DialogGambling gambling)
    {
        for (int i = 0; i < rows; i++)
            AddTradeButton(parent, ref gambling, i);
    }

    private void StartAnimation(ref components.DialogGambling gambling) => gambling.RowAnimationTimeLeft = rowAnimationDelay;
    private void EndAnimation(ref components.DialogGambling gambling) => gambling.RowAnimationTimeLeft = null;
    private void TickAnimation(DefaultEcs.Entity parent, ref components.DialogGambling gambling)
    {
        if (gambling.SelectedCards.Count < rows) {
            Pay(ref gambling);
            PullRandomCard(ref gambling);
            AddTrade(parent, ref gambling, gambling.SelectedCards.Count - 1);
            StartAnimation(ref gambling);
        } else {
            AddBottomButtons(parent, ref gambling);
            AddPurchaseButtons(parent, ref gambling);
            EndAnimation(ref gambling);
        }
    }

    private string GetSpellLabel(SpellRow card)
    {
        var name = card.Name;
        var spellType = card.Type == 0 ? db.GetText(UIDOffensiveSpell).Text : db.GetText(UIDPassiveSpell).Text;
        var spellClass = preload.GetClassText(card.PriceA);
        var prices = preload.GetSpellPrices(card);
        return $"{name}\n{spellType} - {spellClass} - {prices}";
    }

    private string TextSpellCount(SpellRow card) {
        var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
        var count = inventory.CountCards(card.CardId);
        var countInUse = inventory.CountCards(card.CardId, inUse: true);

        if (count == 0){
            return db.GetText(UIDNewSpell).Text.ToUpper(new CultureInfo("en-US", false));
        } else {
            return $"{db.GetText(UIDOldSpell).Text} {db.GetText(UIDOldSpell1).Text} {count}{db.GetText(UIDOldSpell2).Text} {countInUse}";
        }
    }

    private string SpellStats(SpellRow card)
    {
        var isAttack = card.Type == 0;

        var spellCount = TextSpellCount(card);
        var spellType = isAttack ? db.GetText(UIDOffensiveSpell).Text : db.GetText(UIDPassiveSpell).Text;
        var spellClass = preload.GetClassText(card.PriceA);

        var mana = db.GetText(UIDMana).Text;
        var level = db.GetText(UIDLevel).Text;
        var damage = db.GetText(UIDDamage).Text;
        var loadup = db.GetText(UIDLoadup).Text;

        if (isAttack) {
            return $"{spellCount}\n{spellType} - {spellClass}\n{mana}\n{level}\n{damage}\n{loadup}";
        } else {
            return $"{spellCount}\n{spellType} - {spellClass}\n{mana}\n{level}";
        }
    }

    private string SpellValues(SpellRow card)
    {
        var isAttack = card.Type == 0;

        var mana = "{104}" + (card.Mana == 5 ? "-/-" : $"{card.MaxMana}/{card.MaxMana}");
        var level = preload.GetSpellPrices(card);
        var damage = preload.GetLightsIndicator(card.Damage);
        var loadup = preload.GetLightsIndicator(card.Loadup);

        if (isAttack) {
            return $"{mana}\n{level}\n{damage}\n{loadup}";
        } else {
            return $"{mana}\n{level}";
        }
    }

    private DefaultEcs.Entity CreateSpellProfile(DefaultEcs.Entity parent, ref components.DialogGambling gambling, SpellRow card)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        var isAttack = card.Type == 0;
        Vector2 cardTypeOffset = isAttack ? new(0, 0) : new(0, 28);

        preload.CreateLabel(entity)
            .With(gambling.bgRect.Min + new Vector2(30, 22))
            .With(preload.Fnt001)
            .WithText(db.GetText(UIDSpellProfile).Text)
            .Build();

        preload.CreateImage(entity)
            .With(gambling.bgRect.Min + cardTypeOffset + new Vector2(90, 76))
            .With(card.CardId)
            .Build();

        preload.CreateLabel(entity)
            .With(gambling.bgRect.Min + cardTypeOffset + new Vector2(140, 83))
            .With(preload.Fnt003)
            .WithText(card.Name)
            .Build();

        preload.CreateLabel(entity)
            .With(gambling.bgRect.Min + cardTypeOffset + new Vector2(90, 136))
            .With(preload.Fnt002)
            .WithLineHeight(28)
            .WithText(SpellStats(card))
            .Build();

        preload.CreateLabel(entity)
            .With(gambling.bgRect.Min + cardTypeOffset + new Vector2(180, 192))
            .With(preload.Fnt002)
            .WithLineHeight(28)
            .WithText(SpellValues(card))
            .Build();

        preload.CreateLabel(entity)
            .With(gambling.bgRect.Min + cardTypeOffset + new Vector2(90, isAttack ? 300 : 244))
            .With(preload.Fnt000)
            .WithText(card.Info)
            .WithLineHeight(22)
            .WithLineWrap(gambling.bgRect.Size.X - 100)
            .WithAnimation()
            .Build();

        preload.CreateLabel(entity)
            .With(new Vector2(gambling.bgRect.Center.X - 135, gambling.bgRect.Max.Y - 46))
            .With(preload.Fnt002)
            .WithText(db.GetText(UIDTakeIt).Text)
            .Build();

        preload.CreateButton(entity)
            .With(IDYes)
            .With(new Vector2(gambling.bgRect.Center.X - 50, gambling.bgRect.Max.Y - 60))
            .With(new components.ui.ButtonTiles(5, 6))
            .With(preload.Btn000)
            .Build();

        preload.CreateButton(entity)
            .With(IDNo)
            .With(new Vector2(gambling.bgRect.Center.X, gambling.bgRect.Max.Y - 60))
            .With(new components.ui.ButtonTiles(7, 8))
            .With(preload.Btn000)
            .Build();

        return entity;
    }

    private void PullRandomCard(ref components.DialogGambling gambling)
    {
        Random rnd = new();
        var selectedCardId = gambling.Cards.MinBy(c => rnd.Next());
        var selectedCard = db.Spells.FirstOrDefault(c => c.CardId.EntityId == selectedCardId);
        gambling.SelectedCards.Add(selectedCard);
    }

    private void AddTrade(DefaultEcs.Entity entity, ref components.DialogGambling gambling, int index)
    {
        var card = gambling.SelectedCards[index];
        var offset = gambling.bgRect.Center + new Vector2(-180, -130 + 50 * index);

        if (card == null) {
        preload.CreateLabel(entity)
            .With(offset + new Vector2(40, 16))
            .WithText(db.GetText(UIDBlank).Text)
            .With(preload.Fnt000)
            .Build();
            return;
        };

        preload.CreateImage(entity)
            .With(offset)
            .With(card.CardId)
            .Build();

        preload.CreateLabel(entity)
            .With(offset + new Vector2(49, 7))
            .WithLineHeight(13)
            .WithText(GetSpellLabel(card))
            .With(preload.Fnt002)
            .Build();
    }

    private void AddTradeButton(DefaultEcs.Entity entity, ref components.DialogGambling gambling, int index)
    {
        var card = gambling.SelectedCards[index];
        var offset = gambling.bgRect.Center + new Vector2(-180, -130 + 50 * index);
        if (card == null) return;

        var purchase = new components.ui.ElementId(index);
        preload.CreateButton(entity)
            .With(purchase)
            .With(offset + new Vector2(370, -1))
            .With(new components.ui.ButtonTiles(20, 21))
            .With(preload.Btn001)
            .Build();

        gambling.CardPurchaseButtons[purchase] = card;
    }

    private bool CanAfford(ref components.DialogGambling gambling) =>
        zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>().CountCards(gambling.Currency.CardId) >= pricePerRow * rows;

    private void Pay(ref components.DialogGambling gambling)
    {
        var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
        inventory.RemoveCards(gambling.Currency.CardId, (uint)pricePerRow);
        gambling.CurrencyLabel.Dispose();
        gambling.CurrencyLabel = preload.CreateCurrencyLabel(gambling.Profile, gambling.Currency, inventory);
    }

    private bool HasCloverleaf() =>
        zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>().Contains(db.Items.ElementAt(cloverleafI).CardId);

    private List<int?> CloverleafFilter(List<int?> cards) {
        if (!HasCloverleaf()) return cards;

        List<int?> filteredCards = new();
        Random rnd = new();
        foreach (var card in cards)
            if (card != null || rnd.NextDouble() >= 0.8)
                filteredCards.Add(card);
        return filteredCards;
    }

    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId clickedId)
    {
        var uiEntity = Set.GetEntities()[0];
        ref var gambling = ref uiEntity.Get<components.DialogGambling>();

        if (gambling.CardPurchaseButtons.TryGetValue(clickedId, out var card)) {
            gambling.Purchase = card;
            gambling.Profile.Dispose();
            gambling.Profile = CreateSpellProfile(uiEntity, ref gambling, card);
        }
        else if (clickedId == IDYes) {
            zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>().Add(gambling.Purchase!.CardId);
            gambling.Profile.Dispose();
            gambling.Profile = CreatePrimary(uiEntity, ref gambling, allowPurchaseButtons: false);
        }
        else if (clickedId == IDNo) {
            gambling.Profile.Dispose();
            gambling.Profile = CreatePrimary(uiEntity, ref gambling);
        }
        else if (clickedId == IDRepeat) {
            gambling.SelectedCards = new();
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
            if (gambling.RowAnimationTimeLeft <= 0f) TickAnimation(gambling.Profile, ref gambling);
            gambling.RowAnimationTimeLeft -= timeElapsed;
        }
    }
}
