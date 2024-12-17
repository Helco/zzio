using System;
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

    private record struct DeckTabInfo(
        components.ui.ScrDeck.Tab Type,
        components.ui.ElementId Id,
        int PosY,
        int TileNormal, int TileHovered, int TileActive,
        UID TooltipUID
    );
    private static readonly List<DeckTabInfo> Tabs =
    [
        new(Tab.Fairies,       IDTabFairies,       PosY:  79, TileNormal: 0, TileHovered:  1, TileActive:  2, TooltipUID: new(0x7DB4EEB1)),
        new(Tab.Items,         IDTabItems,         PosY: 123, TileNormal: 3, TileHovered:  4, TileActive:  5, TooltipUID: new(0x93530331)),
        new(Tab.AttackSpells,  IDTabAttackSpells,  PosY: 167, TileNormal: 6, TileHovered:  7, TileActive:  8, TooltipUID: new(0xB5E80331)),
        new(Tab.SupportSpells, IDTabSupportSpells, PosY: 211, TileNormal: 9, TileHovered: 10, TileActive: 11, TooltipUID: new(0x9D0DAD11)),
    ];

    private readonly IAssetRegistry assetRegistry;
    private readonly zzio.db.MappedDB mappedDB;

    public ScrDeck(ITagContainer diContainer) : base(diContainer, BlockFlags.All)
    {
        assetRegistry = diContainer.GetTag<IAssetRegistry>();
        mappedDB = diContainer.GetTag<zzio.db.MappedDB>();
        OnElementDown += HandleElementDown;
        OnRightClick += HandleRightClick;
    }

    protected override void HandleOpen(in messages.ui.OpenDeck message)
    {
        World.Publish(new messages.SpawnSample($"resources/audio/sfx/gui/_g006.wav"));
        var entity = World.CreateEntity();
        entity.Set<components.ui.ScrDeck>();
        ref var deck = ref entity.Get<components.ui.ScrDeck>();

        CreateBackgrounds(entity, ref deck);
        CreateListControls(entity, ref deck);
        CreateTopButtons(preload, entity, inventory, IDOpenDeck);
        CreateDeckSlots(entity, ref deck);

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

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(337, 44))
            .With(UIPreloadAsset.Fnt002)
            .WithText($"{zanzarah.OverworldGame!.GetTag<zzio.Savegame>().pixiesCatched}/30")
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

        deck.TabButtons = new DefaultEcs.Entity[4];
        foreach (var tab in Tabs)
            deck.TabButtons[(int)tab.Type - 1] = preload.CreateButton(entity)
                .With(tab.Id)
                .With(Mid + new Vector2(282, tab.PosY))
                .With(new components.ui.ButtonTiles(tab.TileNormal, tab.TileHovered, tab.TileActive))
                .With(UIPreloadAsset.Btn002)
                .WithTooltip(tab.TooltipUID);

        preload.CreateButton(entity)
            .With(IDSwitchListMode)
            .With(Mid + new Vector2(297, 261))
            .With(new components.ui.ButtonTiles(28, 29))
            .With(UIPreloadAsset.Btn002)
            .WithTooltip(0xA086B911)
            .Build();
    }

    private void CreateDeckSlots(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        deck.DeckSlots = new DefaultEcs.Entity[Inventory.FairySlotCount];
        for (int i = 0; i < Inventory.FairySlotCount; i++)
            deck.DeckSlots[i] = CreateDeckSlot(entity, Mid + new Vector2(31, 60 + 79 * i), FirstFairySlot + i, i);
        SetDeckSlots(ref deck);
    }

    private void SetDeckSlots(ref components.ui.ScrDeck deck)
    {
        for (int i = 0; i < Inventory.FairySlotCount; i++)
            SetDeckSlot(deck.DeckSlots[i], ref deck);
    }

    private static Vector2 DeckSlotPos(int fairyI, int slotI) =>
        Mid + new Vector2(81 + 46 * slotI, 60 + 79 * fairyI);

    private static void SpellMode(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        deck.SpellBackground.Set(components.Visibility.Visible);
        deck.SummaryBackground.Set(components.Visibility.Invisible);
        foreach (var deckCard in deck.DeckSlots)
            SpellMode(ref deckCard.Get<components.ui.Slot>());
    }

    private void InfoMode(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        deck.SpellBackground.Set(components.Visibility.Invisible);
        deck.SummaryBackground.Set(components.Visibility.Visible);
        foreach (var deckCard in deck.DeckSlots)
            InfoMode(ref deckCard.Get<components.ui.Slot>());
    }

    private static Vector2 ListSlotPos(int column, int row) =>
        Mid + new Vector2(322 + column * 42, 70 + row * 43);

    private void CreateListSlots(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck, int columns, int rows = ListRows)
    {
        deck.ListSlots = new DefaultEcs.Entity[rows * columns];
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                var i = y * columns + x;
                deck.ListSlots[i] = CreateListSlot(entity, ListSlotPos(x, y), FirstListCell + i, i);
                if (columns == 1)
                    CreateSlotSummary(deck.ListSlots[i], new(42, 9));
            }
        }
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

    private void SetListSlots(ref components.ui.ScrDeck deck)
    {
        var allCardsOfType = AllCardsOfType(deck);
        var count = allCardsOfType.Count();
        deck.Scroll = Math.Clamp(deck.Scroll, 0, Math.Max(0, count - 1));
        var shownCards = allCardsOfType
            .Skip(deck.Scroll)
            .Take(deck.ListSlots.Length);

        for (var i = 0; i < deck.ListSlots.Length; i++)
        {
            var shownCard = shownCards.ElementAtOrDefault(i);
            if (shownCard != default)
                SetListSlot(deck.ListSlots[i], shownCard);
            else UnsetSlot(ref deck.ListSlots[i].Get<components.ui.Slot>());
        }
    }

    private void RecreateList(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        if (deck.ListSlots != default)
            foreach (var listSlot in deck.ListSlots)
                listSlot.Dispose();
        CreateListSlots(entity, ref deck, columns: deck.IsGridMode ? 6 : 1);
        SetListSlots(ref deck);
    }

    private static bool IsInfoTab(Tab tab) => tab == Tab.Fairies || tab == Tab.Items;
    private static bool IsSpellTab(Tab tab) => tab == Tab.AttackSpells || tab == Tab.SupportSpells;

    private void OpenTab(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck, Tab newTab)
    {
        var oldTab = deck.ActiveTab;
        deck.ActiveTab = newTab;

        if (oldTab != Tab.None)
            deck.TabButtons[(int)oldTab - 1].Remove<components.ui.Active>();
        deck.TabButtons[(int)newTab - 1].Set<components.ui.Active>();

        if (IsInfoTab(newTab) && !IsInfoTab(oldTab))
            InfoMode(entity, ref deck);
        if (IsSpellTab(newTab) && !IsSpellTab(oldTab))
            SpellMode(entity, ref deck);

        RecreateList(entity, ref deck);
    }

    private void HandleElementDown(DefaultEcs.Entity clickedEntity, components.ui.ElementId id)
    {
        var deckEntity = Set.GetEntities()[0];
        ref var deck = ref deckEntity.Get<components.ui.ScrDeck>();

        if (clickedEntity.Has<components.ui.SlotButton>())
        {
            HandleSlotClick(deckEntity, ref deck, clickedEntity.Get<components.Parent>().Entity);
        }
        if (deck.VacatedDeckSlot != default)
            return;

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
            SetListSlots(ref deck);
        }
        else if (id == IDSliderUp)
        {
            deck.Scroll -= deck.IsGridMode ? ListRows : 1;
            UpdateSliderPosition(deck);
            SetListSlots(ref deck);
        }

        if (deck.DraggedCardImage != default)
            return;

        HandleNavClick(id, zanzarah, deckEntity, IDOpenDeck);
    }

    private void UpdateSliderPosition(in components.ui.ScrDeck deck)
    {
        var allCardsCount = AllCardsOfType(deck).Count();
        ref var slider = ref deck.ListSlider.Get<components.ui.Slider>();
        slider = slider with { Current = Vector2.UnitY * Math.Clamp(deck.Scroll / (allCardsCount - 1f), 0, 1f) };
    }

    private void HandleHover(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        var curHovered = World.Has<components.ui.HoveredElement>()
            ? World.Get<components.ui.HoveredElement>()
            : default;

        // Still hovering over the same entity
        if (curHovered.Entity == deck.LastHovered) return;

        // Unhovered an entity
        if (deck.LastHovered != default)
        {
            deck.LastHovered = default;
            CreateStats(entity, ref deck);
            return;
        }

        // Hovered an entity
        deck.LastHovered = curHovered.Entity;
        CreateStats(entity, ref deck);
    }

    private void UpdateScroll(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        var slider = deck.ListSlider.Get<components.ui.Slider>();
        var allCardsCount = AllCardsOfType(deck).Count();
        var newScrollI = (int)MathF.Round(slider.Current.Y * (allCardsCount - 1) * (deck.IsGridMode ? ListRows : 1));
        if (newScrollI != deck.Scroll)
        {
            deck.Scroll = newScrollI;
            SetListSlots(ref deck);
        }
    }

    protected override void Update(float elapsedTime, in DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        HandleHover(entity, ref deck);
        UpdateScroll(entity, ref deck);
        Drag(ref deck);
    }

    protected override void HandleKeyDown(KeyCode key)
    {
        var deckEntity = Set.GetEntities()[0];
        ref var deck = ref deckEntity.Get<components.ui.ScrDeck>();
        base.HandleKeyDown(key);
        if (deck.DraggedCardImage == default)
            HandleNavKeyDown(key, zanzarah, deckEntity, IDOpenDeck);
    }

    protected void HandleRightClick()
    {
        var deckEntity = Set.GetEntities()[0];
        ref var deck = ref deckEntity.Get<components.ui.ScrDeck>();
        if (deck.DraggedCard == default)
            return;
        DropCard(deckEntity, ref deck);
    }

    protected override void HandleScroll(float scrollAmount)
    {
        var deckEntity = Set.GetEntities()[0];
        ref var deck = ref deckEntity.Get<components.ui.ScrDeck>();

        if (scrollAmount < 0)
            deck.Scroll += deck.IsGridMode ? ListRows : 1;
        else
            deck.Scroll -= deck.IsGridMode ? ListRows : 1;
        UpdateSliderPosition(deck);
        SetListSlots(ref deck);
    }
}
