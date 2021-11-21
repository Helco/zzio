using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using zzio;

namespace zzre.game
{
    // in the Zanzarah savegames there are explicit indices stored,
    // so we cannot guerantee that every slot is actually filled
    // and thus we have to do some more complicated management :(

    public partial class Inventory : IReadOnlyCollection<InventoryCard>
    {
        private readonly zzio.db.MappedDB mappedDB;
        private readonly List<InventoryCard?> cards = new List<InventoryCard?>();
        public int Count { get; private set; }

        public Inventory(ITagContainer diContainer, Savegame? savegame = null)
            : this(diContainer.GetTag<zzio.db.MappedDB>(), savegame) { }

        public Inventory(zzio.db.MappedDB mappedDB, Savegame? savegame = null)
        {
            this.mappedDB = mappedDB;
            if (savegame == null)
                return;
            cards = Enumerable.Repeat<InventoryCard?>(null, savegame.inventory.Count).ToList();
            foreach (var card in savegame.inventory)
            {
                var atIndex = card.atIndex;
                if (atIndex < 0 || atIndex >= cards.Count)
                    throw new ArgumentOutOfRangeException("Save inventory card index is not valid");
                cards[(int)atIndex] = card;
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

        public InventoryItem AddItem(int entityId)
        {
            var cardId = new CardId(CardType.Item, entityId);
            if (TryGetCard(cardId, out var previous))
            {
                var previousItem = (InventoryItem)previous;
                previousItem.amount++;
                return previousItem;
            }

            var item = new InventoryItem()
            {
                cardId = cardId,
                dbUID = mappedDB.GetItem(entityId).Uid,
                amount = 1
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
                usageCounter = (uint)GlobalRandom.Get.Next(0, 2 << 16)
            };
            Add(spell);
            return spell;
        }

        public InventoryFairy AddFairy(int entityId)
        {
            var random = GlobalRandom.Get;
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

        public bool TryGetCard(CardId cardId, [NotNullWhen(true)] out InventoryCard card)
        {
            card = cards.FirstOrDefault(c => c?.cardId == cardId)!;
            return card != null;
        }

        public int CountCards(CardId cardId) => cards
            .Where(c => c?.cardId == cardId)
            .Sum(c => (int)c!.amount);

        public IEnumerator<InventoryCard> GetEnumerator() => cards.NotNull().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
