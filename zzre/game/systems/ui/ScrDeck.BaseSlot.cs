using System;
using System.Numerics;
using zzio;

namespace zzre.game.systems.ui;

public partial class ScrDeck
{
    // Provide a size for buttons, including unset ones.
    public Vector2 SlotButtonSize = Vector2.One * 38;

    private DefaultEcs.Entity CreateBaseSlot(
        DefaultEcs.Entity parent,
        Vector2 pos,
        components.ui.ElementId id
    )
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));
        entity.Set(new components.ui.Slot());
        ref var slot = ref entity.Get<components.ui.Slot>();

        slot.buttonId = id;
        slot.button = preload.CreateButton(entity)
            .With(id)
            .With(pos)
            .With(new components.ui.ButtonTiles(-1))
            .With(UIPreloadAsset.Wiz000)
            .Build();

        UnsetSlot(ref slot);
        return entity;
    }

    private void CreateSlotSummary(DefaultEcs.Entity entity, Vector2 offset)
    {
        ref var slot = ref entity.Get<components.ui.Slot>();
        slot.summary = preload.CreateLabel(entity)
            .With(slot.button.Get<Rect>().Min + offset)
            .With(UIPreloadAsset.Fnt002);
    }

    private static UITileSheetAsset.Info TileSheet(CardId cardId) => cardId.Type switch
    {
        CardType.Fairy => UIPreloadAsset.Wiz000,
        CardType.Item => UIPreloadAsset.Itm000,
        CardType.Spell => UIPreloadAsset.Spl000,
        _ => throw new NotSupportedException("Unknown inventory card type")
    };

    private void ChangeTileSheet(DefaultEcs.Entity entity, UITileSheetAsset.Info tileSheetInfo)
    {
        // This is probably highly inefficient?
        preload.UI.GetTag<IAssetRegistry>().LoadUITileSheet(entity, tileSheetInfo);
    }

    private static void SetSummaryOffset(ref components.ui.Slot slot)
    {
        var min = slot.button.Get<Rect>().Min + SummaryOffset(ref slot);
        slot.summary.Set(Rect.FromMinMax(min, min));
    }

    private static Vector2 SummaryOffset(ref components.ui.Slot slot)
    {
        if (slot.type == components.ui.Slot.Type.DeckSlot)
            return new(48, 4);
        if (slot.card!.cardId.Type == CardType.Item)
            return new(42, 18);
        return new(42, 9);
    }

    private void SetSlot(ref components.ui.Slot slot, InventoryCard card)
    {
        slot.card = card;
        slot.button.Set(components.Visibility.Visible);
        ChangeTileSheet(slot.button, TileSheet(card.cardId));
        slot.button.Set(new components.ui.ButtonTiles(card.cardId.EntityId));
        slot.button.Set(CardTooltip(card));

        if (slot.summary != default)
        {
            slot.summary.Set(new components.ui.Label(slot.card switch
            {
                InventoryItem item => FormatSlotSummary(item),
                InventorySpell spell => FormatSlotSummary(spell),
                InventoryFairy fairy => FormatSlotSummary(fairy),
                _ => throw new NotSupportedException("Unknown inventory card type")
            }));
            SetSummaryOffset(ref slot);
        }
    }

    private static void UnsetSlot(ref components.ui.Slot slot)
    {
        slot.card = default;
        slot.button.Set(components.Visibility.Invisible);
        if (slot.button.Has<components.ui.TooltipUID>())
            slot.button.Remove<components.ui.TooltipUID>();
        if (slot.usedMarker != default)
            slot.usedMarker.Set(components.Visibility.Invisible);
        if (slot.summary != default)
            slot.summary.Set(new components.ui.Label(""));
    }

    private string FormatSlotSummary(InventoryFairy fairy)
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
        var levelupXP = inventory.GetLevelupXP(fairy);
        if (levelupXP.HasValue)
        {
            builder.Append("{105}");
            builder.Append(levelupXP.Value + 1);
        }

        return builder.ToString();
    }

    private string FormatSlotSummary(InventoryItem item) => item.amount > 1
        ? $"{item.amount} x {mappedDB.GetItem(item.dbUID).Name}"
        : mappedDB.GetItem(item.dbUID).Name;

    private string FormatSlotSummary(InventorySpell spell)
    {
        var dbSpell = mappedDB.GetSpell(spell.dbUID);
        var mana = dbSpell.Mana == 5 ? "-/-" : $"{spell.mana}/{dbSpell.MaxMana}";
        return $"{dbSpell.Name}\n{{104}}{mana} {UIBuilder.GetSpellPrices(dbSpell)}";
    }
}
