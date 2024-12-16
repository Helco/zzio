using System;
using System.Numerics;
using zzio;

namespace zzre.game.systems.ui;

public partial class ScrDeck
{

    private DefaultEcs.Entity CreateListCard(
        DefaultEcs.Entity parent,
        Vector2 pos,
        components.ui.ElementId id
        )
    {
        var entity = CreateBaseCard(parent, pos, id);
        ref var card = ref entity.Get<components.ui.Card>();

        card.usedMarker = preload.CreateImage(entity)
            .With(pos)
            .With(UIPreloadAsset.Inf000, 16)
            .WithRenderOrder(-1)
            .Invisible()
            .Build();

        return entity;
    }

    private void SetListCard(DefaultEcs.Entity entity, InventoryCard invCard)
    {
        ref var card = ref entity.Get<components.ui.Card>();
        SetCard(ref card, invCard);
        card.usedMarker.Set(!IsDraggable(invCard)
            ? components.Visibility.Visible
            : components.Visibility.Invisible);
        card.button.Set(CardTooltip(invCard));
    }

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
}
