﻿using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using zzio;

using Tab = zzre.game.components.ui.ScrDeck.Tab;
using static zzre.game.systems.ui.InGameScreen;
using Silk.NET.SDL;

namespace zzre.game.systems.ui;

public partial class ScrDeck : BaseScreen<components.ui.ScrDeck, messages.ui.OpenDeck>
{
    private const int ListRows = 6;

    private static readonly UID UIDChooseFairyToSwap = new(0x41912581);
    private static readonly UID[] UIDSpellSlotNames =
    [
        new(0x37697321), // First offensive slot
        new(0x8A717321), // First defensive slot
        new(0x0F207721), // Second offensive slot
        new(0x5C577721)  // Second defensive slot
    ];
    private static readonly UID[] UIDFairyInfoDescriptions =
    [
        new(0x45B032A1), // Current and max HP
        new(0xE58236A1), // Level of your fairy
        new(0xB26B36A1), // XP, current and necessary for next level
        new(0xB26B36A1)
    ];

    private static readonly UID[] UIDStatNames =
    [
        new(0xE946ECA1), // Damage
        new(0x238A3981), // Mana
        new(0x04211121), // Fire Rate
    ];

    private static readonly components.ui.ElementId IDSliderUp = new(1);
    private static readonly components.ui.ElementId IDSliderDown = new(2);
    private static readonly components.ui.ElementId IDSlider = new(3);
    private static readonly components.ui.ElementId IDSwitchListMode = new(4);
    private static readonly components.ui.ElementId IDTabFairies = new(10);
    private static readonly components.ui.ElementId IDTabItems = new(11);
    private static readonly components.ui.ElementId IDTabAttackSpells = new(12);
    private static readonly components.ui.ElementId IDTabSupportSpells = new(13);

    private static readonly components.ui.ElementId FirstFairySlot = new(20);
    private static readonly components.ui.ElementId FirstSpellSlot = new(30);
    private static readonly components.ui.ElementId FirstListCell = new(50);

    private readonly IAssetRegistry assetRegistry;
    private readonly zzio.db.MappedDB mappedDB;

    public ScrDeck(ITagContainer diContainer) : base(diContainer, BlockFlags.All)
    {
        assetRegistry = diContainer.GetTag<IAssetRegistry>();
        mappedDB = diContainer.GetTag<zzio.db.MappedDB>();
        OnElementDown += HandleElementDown;
    }

    protected override void HandleOpen(in messages.ui.OpenDeck message)
    {
        World.Publish(new messages.SpawnSample($"resources/audio/sfx/gui/_g006.wav"));
        var entity = World.CreateEntity();
        entity.Set<components.ui.ScrDeck>();
        ref var deck = ref entity.Get<components.ui.ScrDeck>();
        deck.DeckSlotParents = [];

        CreateBackgrounds(entity, ref deck);
        CreateListControls(entity, ref deck);
        CreateTopButtons(preload, entity, inventory, IDOpenDeck);
        CreateFairySlots(entity, ref deck);

        if (deck.ActiveTab == Tab.None)
            OpenTab(entity, ref deck, Tab.Fairies);
    }

    private void CreateBackgrounds(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        preload.CreateFullBackOverlay(entity);

        preload.CreateImage(entity)
            .With(Mid + new Vector2(268, 0))
            .WithBitmap("dec000")
            .WithRenderOrder(1)
            .Build();

        deck.SpellBackground = preload.CreateImage(entity)
            .With(Mid)
            .WithBitmap("dec001")
            .WithRenderOrder(1);

        deck.SummaryBackground = preload.CreateImage(entity)
            .With(Mid)
            .WithBitmap("dec002")
            .WithRenderOrder(1);

        preload.CreateTooltipTarget(entity)
            .With(Mid + new Vector2(11, 11))
            .WithText("{205} - ")
            .Build();
    }

    private void CreateListControls(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        preload.CreateButton(entity)
            .With(IDSliderUp)
            .With(Mid + new Vector2(592, 73))
            .With(new components.ui.ButtonTiles(16, 17))
            .With(UIPreloadAsset.Btn001)
            .Build();

        preload.CreateButton(entity)
            .With(IDSliderDown)
            .With(Mid + new Vector2(592, 291))
            .With(new components.ui.ButtonTiles(18, 19))
            .With(UIPreloadAsset.Btn001)
            .Build();

        deck.ListSlider = preload.CreateButton(entity)
            .With(IDSlider)
            .With(Rect.FromTopLeftSize(Mid + new Vector2(590, 110), new Vector2(40, 182)))
            .With(new components.ui.ButtonTiles(14, 15))
            .With(UIPreloadAsset.Btn001);
        deck.ListSlider.Set(components.ui.Slider.Vertical);

        deck.ListTabs = new DefaultEcs.Entity[4];
        var tabButtonRect = Rect.FromTopLeftSize(Mid + new Vector2(281, 0f), new Vector2(35, 35));

        deck.ListTabs[(int)Tab.Fairies - 1] = preload.CreateButton(entity)
            .With(IDTabFairies)
            .With(tabButtonRect.OffsettedBy(0, 79))
            .With(new components.ui.ButtonTiles(0, 1, 2))
            .With(UIPreloadAsset.Btn002)
            .WithTooltip(0x7DB4EEB1);

        deck.ListTabs[(int)Tab.Items - 1] = preload.CreateButton(entity)
            .With(IDTabItems)
            .With(tabButtonRect.OffsettedBy(0, 123))
            .With(new components.ui.ButtonTiles(3, 4, 5))
            .With(UIPreloadAsset.Btn002)
            .WithTooltip(0x93530331);

        deck.ListTabs[(int)Tab.AttackSpells - 1] = preload.CreateButton(entity)
            .With(IDTabAttackSpells)
            .With(tabButtonRect.OffsettedBy(0, 167))
            .With(new components.ui.ButtonTiles(6, 7, 8))
            .With(UIPreloadAsset.Btn002)
            .WithTooltip(0xB5E80331);

        deck.ListTabs[(int)Tab.SupportSpells - 1] = preload.CreateButton(entity)
            .With(IDTabSupportSpells)
            .With(tabButtonRect.OffsettedBy(0, 211))
            .With(new components.ui.ButtonTiles(9, 10, 11))
            .With(UIPreloadAsset.Btn002)
            .WithTooltip(0x9D0DAD11);

        preload.CreateButton(entity)
            .With(IDSwitchListMode)
            .With(tabButtonRect.Min + new Vector2(16, 261))
            .With(new components.ui.ButtonTiles(28, 29))
            .With(UIPreloadAsset.Btn002)
            .WithTooltip(0xA086B911)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(337, 42))
            .With(UIPreloadAsset.Fnt002)
            .WithText($"{zanzarah.OverworldGame!.GetTag<zzio.Savegame>().pixiesCatched}/30")
            .Build();
    }

    private void CreateFairySlots(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        for (int i = 0; i < Inventory.FairySlotCount; i++)
        {
            var fairy = inventory.GetFairyAtSlot(i);
            var fairyI = fairy?.cardId.EntityId ?? -1;
            preload.CreateButton(entity)
                .With(FirstFairySlot + i)
                .With(Mid + new Vector2(31, 60 + 79 * i))
                .With(new components.ui.ButtonTiles(fairyI))
                .With(UIPreloadAsset.Wiz000)
                .WithTooltip(UIDChooseFairyToSwap)
                .Build();
        }
    }

    private void ResetDeckSlotParents(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck, bool createNewEntities)
    {
        foreach (var oldParent in deck.DeckSlotParents)
            oldParent.Dispose();

        if (createNewEntities)
        {
            deck.DeckSlotParents = Enumerable
                .Repeat(0, Inventory.FairySlotCount)
                .Select(_ =>
                {
                    var slotParent = World.CreateEntity();
                    slotParent.Set(new components.Parent(entity));
                    return slotParent;
                })
                .ToArray();
        }
        else
            deck.DeckSlotParents = new DefaultEcs.Entity[Inventory.FairySlotCount];
    }

    private static Vector2 DeckSlotPos(int fairyI, int slotI) =>
        Mid + new Vector2(81 + 46 * slotI, 60 + 79 * fairyI);

    private void CreateSpellSlots(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        deck.SpellBackground.Set(components.Visibility.Visible);
        deck.SummaryBackground.Set(components.Visibility.Invisible);

        ResetDeckSlotParents(entity, ref deck, createNewEntities: true);
        var nextElementId = FirstSpellSlot;
        for (int fairyI = 0; fairyI < Inventory.FairySlotCount; fairyI++)
        {
            var fairy = inventory.GetFairyAtSlot(fairyI);
            for (int spellI = 0; spellI < InventoryFairy.SpellSlotCount; spellI++)
            {
                var spell = fairy == null ? null : inventory.GetSpellAtSlot(fairy, spellI);
                preload.CreateButton(deck.DeckSlotParents[fairyI])
                    .With(nextElementId)
                    .With(DeckSlotPos(fairyI, spellI))
                    .With(new components.ui.ButtonTiles(spell?.cardId.EntityId ?? -1))
                    .With(UIPreloadAsset.Spl000)
                    .WithTooltip(UIDSpellSlotNames[spellI])
                    .Build();
                nextElementId += 1;

                var spellReq = fairy == null ? default : fairy.spellReqs[spellI];
                if (spellReq != default)
                    CreateSpellReq(
                        deck.DeckSlotParents[fairyI],
                        spellReq,
                        isAttack: (spellI % 2) == 0,
                        DeckSlotPos(fairyI, spellI) + new Vector2(2, 45));
            }
        }
    }

    private DefaultEcs.Entity CreateSpellReq(DefaultEcs.Entity parent, SpellReq spellReq, bool isAttack, Vector2 pos, int renderOrder = 0)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));
        entity.Set(components.Visibility.Visible);
        entity.Set(components.ui.UIOffset.Center);
        entity.Set(new components.ui.RenderOrder(renderOrder));
        entity.Set(IColor.White);
        assetRegistry.LoadUITileSheet(entity, isAttack ? UIPreloadAsset.Cls001 : UIPreloadAsset.Cls000);

        var tileSize = entity.Get<rendering.TileSheet>().GetPixelSize(0);
        entity.Set(Rect.FromTopLeftSize(pos, tileSize * 3));
        entity.Set(spellReq.Select((req, i) => new components.ui.Tile(
            TileId: (int)req,
            Rect: Rect.FromTopLeftSize(pos + i * new Vector2(8, 5), tileSize)))
            .ToArray());

        return entity;
    }

    private void CreateFairyInfo(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck, int fairyI)
    {
        if (deck.DeckSlotParents[fairyI] != default)
            deck.DeckSlotParents[fairyI].Dispose();
        var slotParent = deck.DeckSlotParents[fairyI] = World.CreateEntity();
        slotParent.Set(new components.Parent(entity));
        var fairy = inventory.GetFairyAtSlot(fairyI);
        if (fairy == null)
            return;

        for (int slotI = 0; slotI < InventoryFairy.SpellSlotCount; slotI++)
        {
            preload.CreateTooltipArea(slotParent)
                .With(FirstSpellSlot + fairyI * InventoryFairy.SpellSlotCount + slotI)
                .With(Rect.FromTopLeftSize(DeckSlotPos(fairyI, slotI), Vector2.One * 40))
                .WithTooltip(UIDFairyInfoDescriptions[slotI])
                .Build();

            var spell = inventory.GetSpellAtSlot(fairy, slotI);
            if (spell == null)
                continue;
            preload.CreateLabel(slotParent)
                .With(DeckSlotPos(fairyI, slotI) + new Vector2(0, 44))
                .With(UIPreloadAsset.Fnt002)
                .WithText(FormatManaAmount(spell))
                .Build();
        }

        preload.CreateLabel(slotParent)
            .With(DeckSlotPos(fairyI, 0))
            .With(UIPreloadAsset.Fnt002)
            .WithText(FormatSummary(inventory, fairy))
            .Build();
    }

    private string FormatManaAmount(InventorySpell spell)
    {
        var dbSpell = mappedDB.GetSpell(spell.dbUID);
        return dbSpell.MaxMana == 5
            ? "{104}-"
            : $"{{104}}{spell.mana}/{dbSpell.MaxMana}";
    }

    private static string FormatSummary(Inventory inv, InventoryFairy fairy)
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(fairy.name);
        builder.Append(' ');

        builder.Append(fairy.status switch
        {
            ZZPermSpellStatus.Poisoned => "{110}",
            ZZPermSpellStatus.Cursed => "{111}",
            ZZPermSpellStatus.Burned => "{115}",
            ZZPermSpellStatus.Frozen => "{114}",
            ZZPermSpellStatus.Silenced => "{112}",
            _ => ""
        });
        builder.Append('\n');

        builder.Append("{100}");
        builder.Append(fairy.currentMHP);
        builder.Append('/');
        builder.Append(fairy.maxMHP);
        if (fairy.currentMHP < 100)
            builder.Append(' ');
        if (fairy.currentMHP < 10)
            builder.Append(' ');
        if (fairy.maxMHP < 100)
            builder.Append(' ');
        // no second space for maxMHP

        builder.Append(" L-");
        builder.Append(fairy.level);
        if (fairy.level < 10)
            builder.Append(' ');

        builder.Append("  {101}");
        builder.Append(fairy.xp);
        var levelupXP = inv.GetLevelupXP(fairy);
        if (levelupXP.HasValue)
        {
            builder.Append("{105}");
            builder.Append(levelupXP.Value + 1);
        }

        return builder.ToString();
    }

    private string FormatSummary(InventoryItem item) => item.amount > 1
        ? $"{item.amount} x {mappedDB.GetItem(item.dbUID).Name}"
        : mappedDB.GetItem(item.dbUID).Name;

    private string FormatSummary(InventorySpell spell)
    {
        var dbSpell = mappedDB.GetSpell(spell.dbUID);
        var mana = dbSpell.Mana == 5 ? "-/-" : $"{spell.mana}/{dbSpell.MaxMana}";
        return $"{dbSpell.Name}\n{{104}}{mana} {UIBuilder.GetSpellPrices(dbSpell)}";
    }

    private void CreateFairyInfos(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        deck.SpellBackground.Set(components.Visibility.Invisible);
        deck.SummaryBackground.Set(components.Visibility.Visible);

        ResetDeckSlotParents(entity, ref deck, createNewEntities: false);
        for (int i = 0; i < Inventory.FairySlotCount; i++)
            CreateFairyInfo(entity, ref deck, i);
    }

    private static void ResetList(ref components.ui.ScrDeck deck)
    {
        var allEntities = new[]
        {
            deck.ListButtons,
            deck.ListSummaries,
            deck.ListUsedMarkers
        }.NotNull().SelectMany();
        foreach (var entity in allEntities)
            entity.Dispose();
        deck.ListButtons = deck.ListSummaries = deck.ListUsedMarkers =
            [];
    }

    private static Vector2 ListCellPos(int column, int row) =>
        Mid + new Vector2(322 + column * 42, 70 + row * 43);

    private static UITileSheetAsset.Info ListTileSheet(in components.ui.ScrDeck deck) => deck.ActiveTab switch
    {
        Tab.Fairies => UIPreloadAsset.Wiz000,
        Tab.Items => UIPreloadAsset.Itm000,
        Tab.SupportSpells => UIPreloadAsset.Spl000,
        Tab.AttackSpells => UIPreloadAsset.Spl000,
        _ => UIPreloadAsset.Wiz000
    };

    private void CreateListCells(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck, int columns, int rows = ListRows)
    {
        var buttonTileSheet = ListTileSheet(deck);
        deck.ListButtons = new DefaultEcs.Entity[rows * columns];
        deck.ListUsedMarkers = new DefaultEcs.Entity[rows * columns];
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                var i = y * columns + x;
                deck.ListButtons[i] = preload.CreateButton(entity)
                    .With(FirstListCell + i)
                    .With(ListCellPos(x, y))
                    .With(new components.ui.ButtonTiles(-1))
                    .With(buttonTileSheet);

                deck.ListUsedMarkers[i] = preload.CreateImage(entity)
                    .With(ListCellPos(x, y))
                    .With(UIPreloadAsset.Inf000, 16)
                    .WithRenderOrder(-1)
                    .Invisible();
            }
        }
    }

    private void CreateRowList(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        ResetList(ref deck);
        CreateListCells(entity, ref deck, columns: 1);
        var summaryOffset = new Vector2(42, deck.ActiveTab == Tab.Items ? 14 : 5);
        deck.ListSummaries = new DefaultEcs.Entity[ListRows];
        for (int i = 0; i < ListRows; i++)
        {
            deck.ListSummaries[i % ListRows] = preload.CreateLabel(entity)
                .With(ListCellPos(column: 0, row: i) + summaryOffset)
                .With(UIPreloadAsset.Fnt002);
        }
    }

    private void CreateGridList(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        ResetList(ref deck);
        CreateListCells(entity, ref deck, columns: 6);
    }

    private IEnumerable<InventoryCard> AllCardsOfType(in components.ui.ScrDeck deck) => deck.ActiveTab switch
    {
        Tab.Items => inventory.Items
            .OrderBy(c => mappedDB.GetItem(c.dbUID).Unknown switch { 1 => 0, 0 => 1, _ => 2 })
            .ThenBy(c => c.cardId.EntityId),
        Tab.Fairies => inventory.Fairies.OrderByDescending(c => c.level),
        Tab.AttackSpells => inventory.AttackSpells.OrderBy(c => c.cardId.EntityId),
        Tab.SupportSpells => inventory.SupportSpells.OrderBy(c => c.cardId.EntityId),
        _ => []
    };

    private bool IsDraggable(InventoryCard card) => card.cardId.Type switch
    {
        CardType.Item => mappedDB.GetItem(card.dbUID).Script.Length != 0,
        CardType.Spell => !card.isInUse,
        CardType.Fairy => !card.isInUse,
        _ => throw new ArgumentException($"Invalid inventory card type: {card.cardId.Type}")
    };

    private components.ui.TooltipUID CardTooltip(InventoryItem item)
        => !IsDraggable(item)
        ? new UID(0x8F4510A1) // item cannot be used
        : new UID(0x75F10CA1); // select item

    private components.ui.TooltipUID CardTooltip(InventoryFairy fairy)
        => !IsDraggable(fairy)
        ? new UID(0x9054EAB1) // fairy is in use
        : new UID(0x00B500A1); // select fairy

    private components.ui.TooltipUID CardTooltip(InventorySpell spell)
        => !IsDraggable(spell)
        ? new UID(0x6B46EEB1) // spell is in use
        : mappedDB.GetSpell(spell.dbUID).Type == 0
        ? new UID(0xDA2B08A1) // select offensive spell
        : new UID(0x93840CA1); // select passive spell

    private components.ui.TooltipUID CardTooltip(InventoryCard card) => card.cardId.Type switch
    {
        CardType.Item => CardTooltip((InventoryItem)card),
        CardType.Spell => CardTooltip((InventorySpell)card),
        CardType.Fairy => CardTooltip((InventoryFairy)card),
        _ => throw new ArgumentException($"Invalid inventory card type: {card.cardId.Type}")
    };

    private void FillList(ref components.ui.ScrDeck deck)
    {
        var allCardsOfType = AllCardsOfType(deck);
        var count = allCardsOfType.Count();
        deck.Scroll = Math.Clamp(deck.Scroll, 0, Math.Max(0, count - 1));
        var shownCards = allCardsOfType
            .Skip(deck.Scroll)
            .Take(deck.ListButtons.Length)
            .ToArray();

        int i;

        for (i = 0; i < shownCards.Length; i++)
        {
            deck.ListButtons[i].Set(components.Visibility.Visible);
            deck.ListButtons[i].Set(ListTileSheet(deck));
            deck.ListButtons[i].Set(new components.ui.ButtonTiles(shownCards[i].cardId.EntityId));
            deck.ListButtons[i].Set(new components.ui.CardButton(shownCards[i]));
            deck.ListButtons[i].Set(CardTooltip(shownCards[i]));
            deck.ListUsedMarkers[i].Set(!IsDraggable(shownCards[i])
                ? components.Visibility.Visible
                : components.Visibility.Invisible);
        }
        for (; i < deck.ListButtons.Length; i++)
        {
            deck.ListButtons[i].Set(components.Visibility.Invisible);
            deck.ListButtons[i].Set(new components.ui.CardButton());
            if (deck.ListButtons[i].Has<components.ui.TooltipUID>())
                deck.ListButtons[i].Remove<components.ui.TooltipUID>();
            deck.ListUsedMarkers[i].Set(components.Visibility.Invisible);
        }

        if (deck.IsGridMode)
            return;
        for (i = 0; i < shownCards.Length; i++)
        {
            var summary = shownCards[i] switch
            {
                InventoryItem item => FormatSummary(item),
                InventorySpell spell => FormatSummary(spell),
                InventoryFairy fairy => FormatSummary(inventory, fairy),
                _ => throw new NotSupportedException("Unknown inventory card type")
            };
            deck.ListSummaries[i].Set(new components.ui.Label(summary));
        }
        for (; i < deck.ListButtons.Length; i++)
            deck.ListSummaries[i].Set(new components.ui.Label(""));
    }

    private void RecreateList(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        if (deck.IsGridMode)
            CreateGridList(entity, ref deck);
        else
            CreateRowList(entity, ref deck);
        FillList(ref deck);
    }

    private static void ResetStats(ref components.ui.ScrDeck deck)
    {
        var allEntities = new[]
            {
                deck.StatsTitle,
                deck.StatsDescriptions,
                deck.StatsLights,
                deck.StatsLevel
            };
        foreach (var entity in allEntities)
            if (entity != default)
                entity.Dispose();
        deck.StatsTitle =
        deck.StatsDescriptions =
        deck.StatsLights =
        deck.StatsLevel = default;
    }

    private string[] FormatStats(InventoryFairy fairy)
    {
        var fairyRow = mappedDB.GetFairy(fairy.dbUID);
        return [fairy.name, fairyRow.Info, "", ""];
    }

    private string[] FormatStats(InventoryItem item)
    {
        var itemRow = mappedDB.GetItem(item.dbUID);
        var count = item.amount != 1 ? $"{{5*x{item.amount}}}" : "";
        return [$"{itemRow.Name} {count}", itemRow.Info, "", ""];
    }

    private string[] FormatStats(InventorySpell spell)
    {
        var spellRow = mappedDB.GetSpell(spell.dbUID);
        if (spellRow.Type == 0)
        {
            var stats = new[] {
                spellRow.Damage + 1,
                spellRow.Mana + 1,
                spellRow.Loadup + 1,
            };
            var descs = new[] {
                mappedDB.GetText(UIDStatNames[0]).Text,
                mappedDB.GetText(UIDStatNames[1]).Text,
                mappedDB.GetText(UIDStatNames[2]).Text,
                spellRow.Info,
            };
            return [
                spellRow.Name,
                String.Join(":\n", descs),
                String.Join("\n", stats.Select(stat => UIBuilder.GetLightsIndicator(stat))),
                $"Level: {UIBuilder.GetSpellPrices(spellRow)}",
            ];
        }
        else
        {
            return [
                spellRow.Name,
                $"{mappedDB.GetText(UIDStatNames[1]).Text}:\n{spellRow.Info}",
                spellRow.Mana != 5 ? UIBuilder.GetLightsIndicator(spellRow.Mana + 1) : "-",
                $"Level: {UIBuilder.GetSpellPrices(spellRow)}",
            ];
        }
    }

    private string[] FormatStats(InventoryCard card)
    => card.cardId.Type switch
    {
        CardType.Item => FormatStats((InventoryItem)card),
        CardType.Spell => FormatStats((InventorySpell)card),
        CardType.Fairy => FormatStats((InventoryFairy)card),
        _ => throw new ArgumentException($"Invalid inventory card type: {card.cardId.Type}")
    };

    private void CreateStats(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        ResetStats(ref deck);
        var card = deck.LastHovered.Get<components.ui.CardButton>().card;
        // Show empty stats for blank buttons
        if (card == default)
            return;
        var summary = FormatStats(card);

        deck.StatsTitle = preload.CreateLabel(entity)
            .With(Mid + new Vector2(320, 350))
            .With(UIPreloadAsset.Fnt000)
            .WithText(summary[0])
            .Build();
        deck.StatsDescriptions = preload.CreateLabel(entity)
            .With(Mid + new Vector2(330, 379))
            .With(UIPreloadAsset.Fnt002)
            .WithText(summary[1])
            .WithLineWrap(260f)
            .Build();
        deck.StatsLights = preload.CreateLabel(entity)
            .With(Mid + new Vector2(380, 379))
            .With(UIPreloadAsset.Fnt002)
            .WithText(summary[2])
            .Build();
        deck.StatsLevel = preload.CreateLabel(entity)
            .With(Mid + new Vector2(473, 379))
            .With(UIPreloadAsset.Fnt002)
            .WithText(summary[3])
            .Build();
    }

    private static bool IsInfoTab(Tab tab) => tab == Tab.Fairies || tab == Tab.Items;
    private static bool IsSpellTab(Tab tab) => tab == Tab.AttackSpells || tab == Tab.SupportSpells;

    private void OpenTab(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck, Tab newTab)
    {
        var oldTab = deck.ActiveTab;
        deck.ActiveTab = newTab;

        if (oldTab != Tab.None)
            deck.ListTabs[(int)oldTab - 1].Remove<components.ui.Active>();
        deck.ListTabs[(int)newTab - 1].Set<components.ui.Active>();

        if (IsInfoTab(newTab) && !IsInfoTab(oldTab))
            CreateFairyInfos(entity, ref deck);
        if (IsSpellTab(newTab) && !IsSpellTab(oldTab))
            CreateSpellSlots(entity, ref deck);

        RecreateList(entity, ref deck);
    }

    private void TryDragCard(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck, InventoryCard card)
    {
        if (!IsDraggable(card)) return;

        var buttonTileSheet = ListTileSheet(deck);
        if (deck.DraggedCard != default) deck.DraggedCard.Dispose();
        deck.DraggedCard = preload.CreateImage(entity)
            .With(Mid)
            .With(card.cardId)
            .WithRenderOrder(-2)
            .Build();
        deck.DraggedCard.Set(new components.ui.CardButton(card));
        deck.DraggedCard.Set(components.ui.UIOffset.GameUpperLeft);

        if (deck.DraggedOverlay != default) deck.DraggedOverlay.Dispose();
        deck.DraggedOverlay = preload.CreateImage(entity)
            .With(Mid)
            .With(UIPreloadAsset.Dnd000, 0)
            .WithRenderOrder(-3)
            .Build();
        deck.DraggedOverlay.Set(components.ui.UIOffset.GameUpperLeft);
    }

    private void HandleElementDown(DefaultEcs.Entity clickedEntity, components.ui.ElementId id)
    {
        var deckEntity = Set.GetEntities()[0];
        ref var deck = ref deckEntity.Get<components.ui.ScrDeck>();

        if (id == IDSwitchListMode)
        {
            deck.IsGridMode = !deck.IsGridMode;
            RecreateList(deckEntity, ref deck);
        }
        else if (id == IDTabFairies) OpenTab(deckEntity, ref deck, Tab.Fairies);
        else if (id == IDTabItems) OpenTab(deckEntity, ref deck, Tab.Items);
        else if (id == IDTabAttackSpells) OpenTab(deckEntity, ref deck, Tab.AttackSpells);
        else if (id == IDTabSupportSpells) OpenTab(deckEntity, ref deck, Tab.SupportSpells);
        else if (id == IDSliderDown)
        {
            deck.Scroll += deck.IsGridMode ? ListRows : 1;
            UpdateSliderPosition(deck);
            FillList(ref deck);
        }
        else if (id == IDSliderUp)
        {
            deck.Scroll -= deck.IsGridMode ? ListRows : 1;
            UpdateSliderPosition(deck);
            FillList(ref deck);
        }
        else if (id >= FirstListCell && id < FirstListCell + deck.ListButtons.Length)
        {
            if (clickedEntity.TryGet(out components.ui.CardButton cardButton))
                if (cardButton.card != default)
                    TryDragCard(deckEntity, ref deck, cardButton.card);
        }
        else HandleNavClick(id, zanzarah, deckEntity, IDOpenDeck);
    }

    private void UpdateSliderPosition(in components.ui.ScrDeck deck)
    {
        var allCardsCount = AllCardsOfType(deck).Count();
        ref var slider = ref deck.ListSlider.Get<components.ui.Slider>();
        slider = slider with { Current = Vector2.UnitY * Math.Clamp(deck.Scroll / (allCardsCount - 1f), 0, 1f) };
    }

    private void Drag(DefaultEcs.Entity entity)
    {
        var tiles = entity.Get<components.ui.Tile[]>();
        tiles[0].Rect = tiles[0].Rect with { Center = ui.CursorEntity.Get<Rect>().Center };
    }

    private void TryStartDragging(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        var curHovered = World.Has<components.ui.HoveredElement>()
            ? World.Get<components.ui.HoveredElement>()
            : default;
        if (curHovered.Entity == deck.LastHovered)
            return;
        if (deck.LastHovered != default)
        {
            deck.LastHovered = default;
            ResetStats(ref deck);
        }
        if (deck.ListButtons.Contains(curHovered.Entity))
        {
            deck.LastHovered = curHovered.Entity;
            CreateStats(entity, ref deck);
        }
    }

    private void UpdateScroll(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        var slider = deck.ListSlider.Get<components.ui.Slider>();
        var allCardsCount = AllCardsOfType(deck).Count();
        var newScrollI = (int)MathF.Round(slider.Current.Y * (allCardsCount - 1) * (deck.IsGridMode ? ListRows : 1));
        if (newScrollI != deck.Scroll)
        {
            deck.Scroll = newScrollI;
            FillList(ref deck);
        }
    }

    protected override void Update(float elapsedTime, in DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        TryStartDragging(entity, ref deck);
        UpdateScroll(entity, ref deck);

        if (deck.DraggedCard != default)
        {
            Drag(deck.DraggedCard);
            Drag(deck.DraggedOverlay);
        }
    }

    protected override void HandleKeyDown(KeyCode key)
    {
        var deckEntity = Set.GetEntities()[0];
        base.HandleKeyDown(key);
        HandleNavKeyDown(key, zanzarah, deckEntity, IDOpenDeck);
    }
}
