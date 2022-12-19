namespace zzre.game;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using zzio;

// in the Zanzarah savegames there are explicit indices stored,
// so we cannot guerantee that every slot is actually filled
// and thus we have to do some more complicated management :(

public partial class Inventory : IReadOnlyCollection<InventoryCard>
{
    private readonly zzio.db.MappedDB mappedDB;
    private readonly List<InventoryCard?> cards = new();
    private readonly InventoryFairy?[] fairySlots = new InventoryFairy?[FairySlotCount];
    public int Count { get; private set; }

    public InventoryCard this[int index] => cards[index] ??
        throw new KeyNotFoundException($"Card index {index} is not used in inventory");


    public Inventory(ITagContainer diContainer, Savegame? savegame = null)
        : this(diContainer.GetTag<zzio.db.MappedDB>(), savegame) { }

    public Inventory(zzio.db.MappedDB mappedDB, Savegame? savegame = null)
    {
        this.mappedDB = mappedDB;
        if (savegame == null)
            return;
        var maxIndex = savegame.inventory.Max(i => i.atIndex);
        cards = Enumerable.Repeat<InventoryCard?>(null, (int)maxIndex + 1).ToList();
        foreach (var card in savegame.inventory)
        {
            var atIndex = card.atIndex;
            if (atIndex < 0 || atIndex >= cards.Count)
                throw new ArgumentOutOfRangeException("Save inventory card index is not valid");
            cards[(int)atIndex] = card;
        }

        foreach (var fairy in cards.OfType<InventoryFairy>())
        {
            Update(fairy);
            if (fairy.slotIndex >= 0)
                fairySlots[fairy.slotIndex] = fairy;
        }

        Count = cards.NotNull().Count();
    }

    public void Add(InventoryCard card)
    {
        if (card.cardId.Type == CardType.Item && TryGetCard(card.cardId, out var previous))
        {
            ((InventoryItem)previous).amount += card.amount;
            return;
        }

        Count++;
        card.amount = 1;
        var freeIndex = cards.IndexOf(c => c == null);
        if (freeIndex < 0)
        {
            card.atIndex = (uint)cards.Count;
            cards.Add(card);
        }
        else
        {
            card.atIndex = (uint)freeIndex;
            cards[freeIndex] = card;
        }
    }

    public InventoryCard Add(CardId cardId) => cardId.Type switch
    {
        CardType.Item => AddItem(cardId.EntityId),
        CardType.Spell => AddSpell(cardId.EntityId),
        CardType.Fairy => AddFairy(cardId.EntityId),
        _ => throw new ArgumentException($"Invalid inventory card type: {cardId.Type}")
    };

    public InventoryItem AddItem(int entityId, int amount = 1)
    {
        if (amount < 1)
            throw new ArgumentOutOfRangeException(nameof(amount), "Cannot remove items with Add methods");

        var cardId = new CardId(CardType.Item, entityId);
        if (TryGetCard(cardId, out var previous))
        {
            var previousItem = (InventoryItem)previous;
            previousItem.amount += (uint)amount;
            return previousItem;
        }

        var item = new InventoryItem()
        {
            cardId = cardId,
            dbUID = mappedDB.GetItem(entityId).Uid,
            amount = (uint)amount
        };
        Add(item);
        return item;
    }

    public InventorySpell AddSpell(int entityId)
    {
        var dbRow = mappedDB.GetSpell(entityId);
        var spell = new InventorySpell()
        {
            cardId = new CardId(CardType.Spell, entityId),
            dbUID = dbRow.Uid,
            amount = 1,
            mana = dbRow.MaxMana,
            usageCounter = (uint)Random.Shared.Next(0, 2 << 16)
        };
        Add(spell);
        return spell;
    }

    public InventoryFairy AddFairy(int entityId)
    {
        var random = Random.Shared;
        var dbRow = mappedDB.GetFairy(entityId);
        var fairy = new InventoryFairy()
        {
            cardId = new CardId(CardType.Fairy, entityId),
            dbUID = dbRow.Uid,
            amount = 1,
            slotIndex = -1,
            name = dbRow.Name,
            levelChangeCount = (uint)random.Next(0, 2 << 16),
            xpChangeCount = (uint)random.Next(0, 2 << 16)
        };
        Add(fairy);
        SetLevel(fairy, 0);
        fairy.spellReqs[0] = new SpellReq(dbRow.Class0);
        return fairy;
    }

    public void RemoveCards(CardId cardId, uint maxCount, bool? inUse = null)
    {
        var cardSlots = cards
            .Where(c => c?.cardId == cardId)
            .Where(c => inUse == null || c!.isInUse == inUse.Value);

        for (int i = 0; i < cards.Count; i++)
        {
            if (maxCount == 0)
                break;
            if (cards[i]?.cardId != cardId)
                continue;
            if (inUse != null && cards[i]!.isInUse != inUse.Value)
                continue;

            if (cards[i]!.amount <= maxCount)
            {
                maxCount -= cards[i]!.amount;
                cards[i] = null;
            }
            else
            {
                var removeCount = Math.Min(maxCount, cards[i]!.amount);
                maxCount -= removeCount;
                cards[i]!.amount -= removeCount;
            }
        }
    }

    public bool TryGetCard(CardId cardId, [NotNullWhen(true)] out InventoryCard card)
    {
        card = cards.FirstOrDefault(c => c?.cardId == cardId)!;
        return card != null;
    }

    public int IndexOf(InventoryCard card) => cards.IndexOf(card);

    public int CountCards(CardId cardId, bool? inUse = null) => cards
        .Where(c => c?.cardId == cardId)
        .Where(c => inUse == null || c!.isInUse == inUse.Value)
        .Sum(c => (int)c!.amount);

    public InventoryFairy? GetFairyAtSlot(int slot) => fairySlots[slot];

    public InventorySpell? GetSpellAtSlot(InventoryFairy fairy, int slot)
    {
        var spellI = fairy.spellIndices[slot];
        return spellI < 0 ? null : (InventorySpell)cards[spellI]!;
    }

    public IEnumerable<InventoryItem> Items => this.OfType<InventoryItem>();
    public IEnumerable<InventoryFairy> Fairies => this.OfType<InventoryFairy>();
    public IEnumerable<InventorySpell> Spells => this.OfType<InventorySpell>();

    public IEnumerable<InventorySpell> AttackSpells =>
        Spells.Where(s => mappedDB.GetSpell(s.dbUID).Type == 0);
    public IEnumerable<InventorySpell> SupportSpells =>
        Spells.Where(s => mappedDB.GetSpell(s.dbUID).Type != 0);

    public InventoryFairy? ActiveOverworldFairy => fairySlots
        .Where(f => f?.currentMHP > 0)
        .FirstOrDefault();

    public IEnumerator<InventoryCard> GetEnumerator() => cards.NotNull().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
