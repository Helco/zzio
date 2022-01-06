using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using zzio;
using zzre.game.messages.ui;

using Tab = zzre.game.components.ui.ScrDeck.Tab;
using static zzre.game.systems.ui.InGameScreen;
using Veldrid;

namespace zzre.game.systems.ui
{
    public partial class ScrDeck : BaseScreen<components.ui.ScrDeck, messages.ui.OpenDeck>
    {
        private const int ListRows = 6;

        private static readonly UID UIDChooseFairyToSwap = new UID(0x41912581);
        private static readonly UID[] UIDSpellSlotNames = new UID[]
        {
            new(0x37697321), // First offensive slot
            new(0x8A717321), // First defensive slot
            new(0x0F207721), // Second offensive slot
            new(0x5C577721)  // Second defensive slot
        };
        private static readonly UID[] UIDFairyInfoDescriptions = new UID[]
        {
            new(0x45B032A1), // Current and max HP
            new(0xE58236A1), // Level of your fairy
            new(0xB26B36A1), // XP, current and necessary for next level
            new(0xB26B36A1)
        };

        private static readonly components.ui.ElementId IDSliderUp = new(1);
        private static readonly components.ui.ElementId IDSliderDown = new(2);
        private static readonly components.ui.ElementId IDSlider = new(3);
        private static readonly components.ui.ElementId IDSwitchListMode = new(4);
        private static readonly components.ui.ElementId IDTabFairies = new(10);
        private static readonly components.ui.ElementId IDTabItems = new(11);
        private static readonly components.ui.ElementId IDTabAttackSpells = new(12);
        private static readonly components.ui.ElementId IDTabSupportSpells = new(13);

        private static readonly components.ui.ElementId FirstFairySlot = new(20);
        private static readonly components.ui.ElementId LastFairySlot = new(19 + Inventory.FairySlotCount);
        private static readonly components.ui.ElementId FirstSpellSlot = new(30);
        private static readonly components.ui.ElementId LastSpellSlot = new(29 + Inventory.FairySlotCount * InventoryFairy.SpellSlotCount);
        private static readonly components.ui.ElementId FirstListCell = new(50);
        private static readonly components.ui.ElementId LastListCell = new(49 + 6 * 6);

        private readonly zzio.db.MappedDB mappedDB;

        public ScrDeck(ITagContainer diContainer) : base(diContainer)
        {
            mappedDB = diContainer.GetTag<zzio.db.MappedDB>();
            OnElementDown += HandleElementDown;
        }

        protected override void HandleOpen(in OpenDeck message)
        {
            var entity = World.CreateEntity();
            entity.Set<components.ui.ScrDeck>();
            ref var deck = ref entity.Get<components.ui.ScrDeck>();
            deck.Inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
            deck.DeckSlotParents = Array.Empty<DefaultEcs.Entity>();

            CreateBackgrounds(entity, ref deck);
            CreateListControls(entity, ref deck);
            CreateTopButtons(preload, entity, IDOpenDeck);
            CreateFairySlots(entity, ref deck);

            if (deck.ActiveTab == Tab.None)
                OpenTab(entity, ref deck, Tab.Fairies);
        }

        private void CreateBackgrounds(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
        {
            preload.CreateImage(
                entity,
                - new Vector2(52, 240),
                "dec000",
                renderOrder: 1);

            deck.SpellBackground = preload.CreateImage(
                entity,
                - new Vector2(320, 240),
                "dec001",
                renderOrder: 1);

            deck.SummaryBackground = preload.CreateImage(
                entity,
                - new Vector2(320, 240),
                "dec002",
                renderOrder: 1);

            preload.CreateTooltip(entity, new Vector2(-320 + 11, -240 + 11), "{205} - ");
        }

        private void CreateListControls(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
        {
            preload.CreateImageButton(
                entity,
                IDSliderUp,
                Mid + new Vector2(592, 73),
                new(16, 17),
                preload.Btn001);

            preload.CreateImageButton(
                entity,
                IDSliderDown,
                Mid + new Vector2(592, 291),
                new(18, 19),
                preload.Btn001);

            deck.ListSlider = preload.CreateImageButton(
                entity,
                IDSlider,
                Rect.FromTopLeftSize(Mid + new Vector2(590, 110), new Vector2(40, 182)),
                new(14, 15),
                preload.Btn001);
            deck.ListSlider.Set(components.ui.Slider.Vertical);

            deck.ListTabs = new DefaultEcs.Entity[4];
            var tabButtonRect = Rect.FromTopLeftSize(Mid + new Vector2(281, 0f), new Vector2(35, 35));
            deck.ListTabs[(int)Tab.Fairies - 1] = preload.CreateImageButton(
                entity,
                IDTabFairies,
                tabButtonRect.OffsettedBy(0, 79),
                new(0, 1, 2),
                preload.Btn002,
                tooltipUID: new UID(0x7DB4EEB1));

            deck.ListTabs[(int)Tab.Items - 1] = preload.CreateImageButton(
                entity,
                IDTabItems,
                tabButtonRect.OffsettedBy(0, 123),
                new(3, 4, 5),
                preload.Btn002,
                tooltipUID: new UID(0x93530331));

            deck.ListTabs[(int)Tab.AttackSpells - 1] = preload.CreateImageButton(
                entity,
                IDTabAttackSpells,
                tabButtonRect.OffsettedBy(0, 167),
                new(6, 7, 8),
                preload.Btn002,
                tooltipUID: new UID(0xB5E80331));

            deck.ListTabs[(int)Tab.SupportSpells - 1] = preload.CreateImageButton(
                entity,
                IDTabSupportSpells,
                tabButtonRect.OffsettedBy(0, 211),
                new(9, 10, 11),
                preload.Btn002,
                tooltipUID: new UID(0x9D0DAD11));

            preload.CreateImageButton(
                entity,
                IDSwitchListMode,
                tabButtonRect.Min + new Vector2(15, 261),
                new(28, 29),
                preload.Btn002,
                tooltipUID: new UID(0xA086B911));

            // TODO: Add pixie count label
        }

        private void CreateFairySlots(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
        {
            for (int i = 0; i < Inventory.FairySlotCount; i++)
            {
                var fairy = deck.Inventory.GetFairyAtSlot(i);
                var fairyI = fairy?.cardId.EntityId ?? -1;
                preload.CreateImageButton(
                    entity,
                    FirstFairySlot + i,
                    Mid + new Vector2(31, 60 + 79 * i),
                    new(fairyI),
                    preload.Wiz000,
                    tooltipUID: UIDChooseFairyToSwap);
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
                var fairy = deck.Inventory.GetFairyAtSlot(fairyI);
                for (int spellI = 0; spellI < InventoryFairy.SpellSlotCount; spellI++)
                {
                    var spell = fairy == null ? null : deck.Inventory.GetSpellAtSlot(fairy, spellI);
                    preload.CreateImageButton(
                        deck.DeckSlotParents[fairyI],
                        nextElementId,
                        DeckSlotPos(fairyI, spellI),
                        new(spell?.cardId.EntityId ?? -1),
                        preload.Spl000,
                        tooltipUID: UIDSpellSlotNames[spellI]);
                    nextElementId = nextElementId + 1;

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
            entity.Set(isAttack ? preload.Cls001 : preload.Cls000);

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
            var fairy = deck.Inventory.GetFairyAtSlot(fairyI);
            if (fairy == null)
                return;

            for (int slotI = 0; slotI < InventoryFairy.SpellSlotCount; slotI++)
            {
                preload.CreateTooltipArea(
                    slotParent,
                    FirstSpellSlot + fairyI * InventoryFairy.SpellSlotCount + slotI,
                    Rect.FromTopLeftSize(DeckSlotPos(fairyI, slotI), Vector2.One * 40),
                    UIDFairyInfoDescriptions[slotI]);

                var spell = deck.Inventory.GetSpellAtSlot(fairy, slotI);
                if (spell == null)
                    continue;
                preload.CreateLabel(
                    slotParent,
                    DeckSlotPos(fairyI, slotI) + new Vector2(0, 44),
                    FormatManaAmount(spell),
                    preload.Fnt002);
            }

            preload.CreateLabel(
                slotParent,
                DeckSlotPos(fairyI, 0),
                FormatSummary(deck.Inventory, fairy),
                preload.Fnt002);
        }

        private string FormatManaAmount(InventorySpell spell)
        {
            var dbSpell = mappedDB.GetSpell(spell.dbUID);
            return dbSpell.MaxMana == 5
                ? "{104}-"
                : $"{{104}}{spell.mana}/{dbSpell.MaxMana}";
        }

        private string FormatSummary(Inventory inv, InventoryFairy fairy)
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
                builder.Append (' ');

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
            var sheet = dbSpell.Type == 0 ? '5' : '4';
            return $"{dbSpell.Name}\n{{104}}{mana} {{{sheet}{dbSpell.PriceA}}}{{{sheet}{dbSpell.PriceB}}}{{{sheet}{dbSpell.PriceC}}}";
        }

        private void CreateFairyInfos(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
        {
            deck.SpellBackground.Set(components.Visibility.Invisible);
            deck.SummaryBackground.Set(components.Visibility.Visible);

            ResetDeckSlotParents(entity, ref deck, createNewEntities: false);
            for (int i = 0; i < Inventory.FairySlotCount; i++)
                CreateFairyInfo(entity, ref deck, i);
        }

        private void ResetList(ref components.ui.ScrDeck deck)
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
                Array.Empty<DefaultEcs.Entity>();
        }

        private Vector2 ListCellPos(int column, int row) =>
            Mid + new Vector2(322 + column * 42, 70 + row * 43);

        private DefaultEcs.Resource.ManagedResource<resources.UITileSheetInfo, rendering.TileSheet> ListTileSheet(in components.ui.ScrDeck deck) => deck.ActiveTab switch
        {
            Tab.Fairies => preload.Wiz000,
            Tab.Items => preload.Itm000,
            Tab.SupportSpells => preload.Spl000,
            Tab.AttackSpells => preload.Spl000,
            _ => preload.Wiz000
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
                    deck.ListButtons[i] = preload.CreateImageButton(
                        entity,
                        FirstListCell + i,
                        ListCellPos(x, y),
                        new(-1),
                        buttonTileSheet);

                    deck.ListUsedMarkers[i] = preload.CreateImage(
                        entity,
                        ListCellPos(x, y),
                        preload.Inf000,
                        tileI: 16,
                        renderOrder: -1);
                    deck.ListUsedMarkers[i].Set(components.Visibility.Invisible);
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
                deck.ListSummaries[i % ListRows] = preload.CreateLabel(
                    entity,
                    ListCellPos(column: 0, row: i) + summaryOffset,
                    "",
                    preload.Fnt002);
            }
        }

        private void CreateGridList(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
        {
            ResetList(ref deck);
            CreateListCells(entity, ref deck, columns: 6);
        }

        private IEnumerable<InventoryCard> AllCardsOfType(in components.ui.ScrDeck deck) => deck.ActiveTab switch
        {
            Tab.Items => deck.Inventory.Items,
            Tab.Fairies => deck.Inventory.Fairies,
            Tab.AttackSpells => deck.Inventory.AttackSpells,
            Tab.SupportSpells => deck.Inventory.SupportSpells,
            _ => Enumerable.Empty<InventoryCard>()
        };

        private void FillList(ref components.ui.ScrDeck deck)
        {
            var allCardsOfType = AllCardsOfType(deck);
            var count = allCardsOfType.Count();
            deck.Scroll = Math.Clamp(deck.Scroll, 0, count - 1);
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
                deck.ListUsedMarkers[i].Set(shownCards[i].isInUse
                    ? components.Visibility.Visible
                    : components.Visibility.Invisible);
            }
            for (; i < deck.ListButtons.Length; i++)
            {
                deck.ListButtons[i].Set(components.Visibility.Invisible);
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
                    InventoryFairy fairy => FormatSummary(deck.Inventory, fairy),
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

        private static bool IsInfoTab(Tab tab) => tab == Tab.Fairies || tab == Tab.Items;
        private static bool IsSpellTab(Tab tab) => tab == Tab.AttackSpells|| tab == Tab.SupportSpells;

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
        }

        private void UpdateSliderPosition(in components.ui.ScrDeck deck)
        {
            var allCardsCount = AllCardsOfType(deck).Count();
            ref var slider = ref deck.ListSlider.Get<components.ui.Slider>();
            slider = slider with { Current = Vector2.UnitY * Math.Clamp(deck.Scroll / (allCardsCount - 1f), 0, 1f) };
        }

        protected override void Update(
            float elapsedTime,
            in DefaultEcs.Entity entity,
            ref components.ui.ScrDeck deck)
        {
            var slider = deck.ListSlider.Get<components.ui.Slider>();
            var allCardsCount = AllCardsOfType(deck).Count();
            var newScrollI = (int)MathF.Round(slider.Current.Y * (allCardsCount - 1));
            if (newScrollI != deck.Scroll)
            {
                deck.Scroll = newScrollI;
                FillList(ref deck);
            }
        }

        protected override void HandleKeyDown(Key key)
        {
            base.HandleKeyDown(key);

            if (key == Key.Enter)
            {
                foreach (var entity in Set.GetEntities())
                    entity.Dispose();
            }
        }
    }
}
