using System;
using System.Linq;
using DefaultEcs.System;
using zzio;
using zzio.db;

namespace zzre.game.systems;

partial class DialogScript
{
    private Inventory PlayerInventory => game.PlayerEntity.Get<Inventory>();

    private bool IfPlayerHasCards(DefaultEcs.Entity entity, int count, CardType type, int id)
    {
        if (type == CardType.Fairy && count >= 0)
        {
            var firstFairy = PlayerInventory.GetFairyAtSlot(0);
            return firstFairy != null && firstFairy.cardId.EntityId == id;
        }

        var actualCount = PlayerInventory.CountCards(new(type, id), inUse: false);
        return count switch
        {
            _ when count < 0 => actualCount > 0,
            _ when count == 0 => actualCount == 0,
            _ => actualCount >= count
        };
    }

    private bool IfPlayerHasSpecials(DefaultEcs.Entity entity, SpecialInventoryCheck specialType, int arg)
    {
        switch (specialType)
        {
            case SpecialInventoryCheck.HasFivePixies:
                if (savegame.pixiesHolding < 5)
                    return false;
                savegame.pixiesHolding -= 5;
                return true;

            case SpecialInventoryCheck.HasAFairy:
                return (PlayerInventory.GetFairyAtSlot(0) != null) == (arg != 0);

            case SpecialInventoryCheck.HasAtLeastNFairies:
                return PlayerInventory.Fairies.Count() >= arg;

            case SpecialInventoryCheck.HasFairyOfClass:
                return PlayerInventory.Fairies.Any(f => db.GetFairy(f.dbUID).Class0 == (ZZClass)arg);

            default: throw new NotSupportedException($"Unsupported special inventory check {specialType}");
        }
    }

    private void GivePlayerCards(DefaultEcs.Entity entity, int count, CardType type, int id)
    {
        for (int i = 0; i < count; i++)
        {
            var invCard = PlayerInventory.Add(new CardId(type, id));
            World.Publish(new messages.GotCard(invCard.dbUID, count));
            if (invCard is not InventoryFairy invFairy)
                continue;

            var dbFairy = db.GetFairy(invFairy.dbUID);
            var isFirstFairy = PlayerInventory.SetFirstFreeSlot(invFairy) == 0;
            var spellId = isFirstFairy
                ? (int)StdSpells.GetStarterSpellFor(dbFairy.Class0)
                : StdSpells.GetFirstAttackSpell(db, dbFairy.Class0, (uint)Random.Shared.Next(3, 10)).CardId.EntityId;
            var invSpell = PlayerInventory.AddSpell(spellId);
            PlayerInventory.SetSpellSlot(invFairy, invSpell, 0);

            PlayerInventory.AddXP(invFairy,
                PlayerInventory.GetXPForLevel(invFairy, isFirstFairy ? 9u : 3u));
        }
    }

    private void RemovePlayerCards(DefaultEcs.Entity entity, int count, CardType type, int id)
    {
        PlayerInventory.RemoveCards(new(type, id), (uint)count, inUse: false);
    }

    private void RemoveWizforms(DefaultEcs.Entity entity)
    {
        NPCEntity.Get<Inventory>().ClearDeck();
    }

    private void Revive(DefaultEcs.Entity entity) => PlayerInventory.FillMana();
    private const int MaxPresentFairyId = 77;
    private void GivePlayerPresent(DefaultEcs.Entity entity)
    {
        // TODO: Verify for givePlayerPresent whether the DB query iteration is actually ordered

        var newDbFairy = db.Fairies
            .Where(f => f.CardId.EntityId < MaxPresentFairyId)
            .OrderBy(f => f.CardId.EntityId)
            .FirstOrDefault(f => !PlayerInventory.TryGetCard(f.CardId, out _));
        if (newDbFairy == null)
            GivePlayerCards(entity, 20, CardType.Item, (int)StdItemId.GoldenCarrot);
        else
            GivePlayerCards(entity, 1, CardType.Fairy, newDbFairy.CardId.EntityId);
    }
}
