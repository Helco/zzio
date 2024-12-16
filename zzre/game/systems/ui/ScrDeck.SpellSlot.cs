using System.Numerics;
using System.Linq;
using zzio;

namespace zzre.game.systems.ui;

public partial class ScrDeck
{
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

    private DefaultEcs.Entity CreateSpellSlot(DefaultEcs.Entity parent, ref components.ui.Slot slot, int index)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));
        entity.Set(new components.ui.SpellSlot());
        ref var spellSlot = ref entity.Get<components.ui.SpellSlot>();
        spellSlot.index = index;

        spellSlot.button = preload.CreateButton(entity)
            .With(slot.buttonId + index + 1)
            .With(slot.button.Get<Rect>().Min + new Vector2(81 + 46 * spellSlot.index))
            .With(new components.ui.ButtonTiles(-1))
            .With(UIPreloadAsset.Spl000)
            // .WithTooltip(UIDSpellSlotNames[i])
            .Build();

        InfoMode(ref spellSlot);

        return entity;
    }

    public void SetSpell(DefaultEcs.Entity entity, ref components.ui.SpellSlot spellSlot, InventorySpell invSpell)
    {
        spellSlot.spell = invSpell;
        spellSlot.button.Set(new components.ui.ButtonTiles(invSpell.cardId.EntityId));
        if (spellSlot.summary != default) spellSlot.summary.Dispose();
        spellSlot.summary = preload.CreateLabel(entity)
            .With(spellSlot.button.Get<Rect>().Min + new Vector2(0, 44))
            .With(UIPreloadAsset.Fnt002)
            .WithText(FormatManaAmount(spellSlot.spell))
            .Build();
        if (spellSlot.req != default) spellSlot.req.Dispose();
        spellSlot.req = CreateSpellReq(
            entity,
            ((InventoryFairy)(entity.Get<components.Parent>().Entity.Get<components.ui.Slot>().card!)).spellReqs[spellSlot.index],
            (spellSlot.index % 2) == 0,
            spellSlot.button.Get<Rect>().Min + new Vector2(2, 45)
        );
    }

    public static void InfoMode(ref components.ui.SpellSlot spellSlot)
    {
        spellSlot.button.Set(components.Visibility.Invisible);
        spellSlot.button.Set(new components.ui.TooltipUID(UIDFairyInfoDescriptions[spellSlot.index]));
        if (spellSlot.summary != default)
            spellSlot.summary.Set(components.Visibility.Visible);
        if (spellSlot.req != default)
            spellSlot.req.Set(components.Visibility.Invisible);
    }

    public static void SpellMode(ref components.ui.SpellSlot spellSlot)
    {
        spellSlot.button.Set(components.Visibility.Visible);
        spellSlot.button.Set(new components.ui.TooltipUID(UIDSpellSlotNames[spellSlot.index]));
        if (spellSlot.summary != default)
            spellSlot.summary.Set(components.Visibility.Invisible);
        if (spellSlot.req != default)
            spellSlot.req.Set(components.Visibility.Visible);
    }

    private DefaultEcs.Entity CreateSpellReq(DefaultEcs.Entity parent, SpellReq spellReq, bool isAttack, Vector2 pos)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));
        entity.Set(components.Visibility.Visible);
        entity.Set(components.ui.UIOffset.Center);
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

    private string FormatManaAmount(InventorySpell spell)
    {
        var dbSpell = mappedDB.GetSpell(spell.dbUID);
        return dbSpell.MaxMana == 5
            ? "{104}-"
            : $"{{104}}{spell.mana}/{dbSpell.MaxMana}";
    }
}
