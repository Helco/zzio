using System;
using System.Numerics;
using System.Linq;
using zzio;
using static zzre.game.systems.ui.InGameScreen;

namespace zzre.game.systems.ui;

public partial class ScrDeck
{
    private static readonly UID[] UIDStatNames =
    [
        new(0xE946ECA1), // Damage
        new(0x238A3981), // Mana
        new(0x04211121), // Fire Rate
    ];
    private static readonly UID numCollectedFairies = new(0xA3EC01B1);

    private void CreateStats(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        var card = GetHoveredCard(entity, ref deck);
        var summary = card == default ? GetDefaultSummary(ref deck) : FormatStats(card);

        if (deck.StatsTitle != default) deck.StatsTitle.Dispose();
        deck.StatsTitle = preload.CreateLabel(entity)
            .With(Mid + new Vector2(320, 350))
            .With(UIPreloadAsset.Fnt000)
            .WithText(summary[0])
            .Build();
        if (deck.StatsDescriptions != default) deck.StatsDescriptions.Dispose();
        deck.StatsDescriptions = preload.CreateLabel(entity)
            .With(Mid + new Vector2(330, 379))
            .With(UIPreloadAsset.Fnt002)
            .WithText(summary[1])
            .WithLineWrap(260f)
            .Build();
        if (deck.StatsLights != default) deck.StatsLights.Dispose();
        deck.StatsLights = preload.CreateLabel(entity)
            .With(Mid + new Vector2(380, 379))
            .With(UIPreloadAsset.Fnt002)
            .WithText(summary[2])
            .Build();
        if (deck.StatsLevel != default) deck.StatsLevel.Dispose();
        deck.StatsLevel = preload.CreateLabel(entity)
            .With(Mid + new Vector2(473, 379))
            .With(UIPreloadAsset.Fnt002)
            .WithText(summary[3])
            .Build();
    }

    private static InventoryCard? GetHoveredCard(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
    {
        // Hovering over a non-slot entity
        if (!deck.LastHovered.Has<components.ui.SlotButton>()) return default;

        ref var slot = ref deck.LastHovered.Get<components.Parent>().Entity.Get<components.ui.Slot>();

        // Hovering over an empty slot entity
        if (slot.card == default)
            return default;

        // Hovering over deck slot while in fairy tab (get "faery count" instead)
        if (deck.ActiveTab == components.ui.ScrDeck.Tab.Fairies && slot.type == components.ui.Slot.Type.DeckSlot)
            return default;

        // Dragging fairy over non-deck slot
        if (deck.DraggedCard?.cardId.Type == CardType.Fairy && slot.type != components.ui.Slot.Type.DeckSlot)
            return default;

        // Dragging spell over non-spell slot
        if (deck.DraggedCard?.cardId.Type == CardType.Spell && slot.type != components.ui.Slot.Type.SpellSlot)
            return default;

        return slot.card;
    }

    private string[] GetDefaultSummary(ref components.ui.ScrDeck deck)
    {
        if (deck.ActiveTab == components.ui.ScrDeck.Tab.Fairies)
            return ["", $"{mappedDB.GetText(numCollectedFairies).Text} {inventory.Fairies.Count()}", "", ""];
        return ["", "", "", ""];
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
}
