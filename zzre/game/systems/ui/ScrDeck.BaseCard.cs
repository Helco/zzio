using System;
using System.Numerics;
using zzio;

namespace zzre.game.systems.ui;

public partial class ScrDeck
{
    private DefaultEcs.Entity CreateBaseCard(
        DefaultEcs.Entity parent,
        Vector2 pos,
        components.ui.ElementId id
    )
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));
        entity.Set(new components.ui.Card());
        ref var card = ref entity.Get<components.ui.Card>();

        card.buttonId = id;
        card.button = preload.CreateButton(entity)
            .With(id)
            .With(pos)
            .With(new components.ui.ButtonTiles(-1))
            .With(UIPreloadAsset.Wiz000)
            .Build();

        UnsetCard(ref card);
        return entity;
    }

    private void CreateCardSummary(DefaultEcs.Entity entity, Vector2 offset)
    {
        ref var card = ref entity.Get<components.ui.Card>();
        card.summary = preload.CreateLabel(entity)
            .With(card.button.Get<Rect>().Min + offset)
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

    private void SetCard(ref components.ui.Card card, InventoryCard invCard)
    {
        card.card = invCard;
        card.button.Set(components.Visibility.Visible);
        ChangeTileSheet(card.button, TileSheet(invCard.cardId));
        card.button.Set(new components.ui.ButtonTiles(invCard.cardId.EntityId));
        card.button.Set(CardTooltip(invCard));

        if (card.summary != default)
            card.summary.Set(new components.ui.Label(card.card switch
            {
                InventoryItem item => FormatSummary(item),
                InventorySpell spell => FormatSummary(spell),
                InventoryFairy fairy => FormatSummary(fairy),
                _ => throw new NotSupportedException("Unknown inventory card type")
            }));
    }

    private static void UnsetCard(ref components.ui.Card card)
    {
        card.card = default;
        card.button.Set(components.Visibility.Invisible);
        if (card.button.Has<components.ui.TooltipUID>())
            card.button.Remove<components.ui.TooltipUID>();
        if (card.usedMarker != default)
            card.usedMarker.Set(components.Visibility.Invisible);
        if (card.summary != default)
            card.summary.Set(new components.ui.Label(""));
    }

    private string FormatSummary(InventoryFairy fairy)
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

    private string FormatSummary(InventoryItem item) => item.amount > 1
        ? $"{item.amount} x {mappedDB.GetItem(item.dbUID).Name}"
        : mappedDB.GetItem(item.dbUID).Name;

    private string FormatSummary(InventorySpell spell)
    {
        var dbSpell = mappedDB.GetSpell(spell.dbUID);
        var mana = dbSpell.Mana == 5 ? "-/-" : $"{spell.mana}/{dbSpell.MaxMana}";
        return $"{dbSpell.Name}\n{{104}}{mana} {UIBuilder.GetSpellPrices(dbSpell)}";
    }

}
