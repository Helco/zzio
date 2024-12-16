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
            .With(slot.button.Get<Rect>().Min + new Vector2(50 + 46 * spellSlot.index, 0))
            .With(new components.ui.ButtonTiles(-1))
            .With(UIPreloadAsset.Spl000)
            // .WithTooltip(UIDSpellSlotNames[i])
            .Build();

        InfoMode(ref spellSlot);

        return entity;
    }

    public void SetSpellSlot(DefaultEcs.Entity entity, InventorySpell invSpell)
    {
        ref var spellSlot = ref entity.Get<components.ui.SpellSlot>();
        spellSlot.spell = invSpell;
        spellSlot.button.Set(new components.ui.ButtonTiles(invSpell.cardId.EntityId));
        CreateSpellSummary(entity);
        CreateSpellReq(entity);
    }

    public void UnsetSpellSlot(DefaultEcs.Entity entity)
    {
        CreateSpellReq(entity);
    }

    public void InfoMode(ref components.ui.SpellSlot spellSlot)
    {
        spellSlot.button.Set(components.Visibility.Invisible);
        spellSlot.button.Set(new components.ui.TooltipUID(UIDFairyInfoDescriptions[spellSlot.index]));
        if (spellSlot.summary != default)
            spellSlot.summary.Set(spellSlot.spell != default
                ? new components.ui.Label(FormatManaAmount(spellSlot.spell))
                : new components.ui.Label(""));
        if (spellSlot.req != default)
            spellSlot.req.Set(components.Visibility.Invisible);
    }

    public static void SpellMode(ref components.ui.SpellSlot spellSlot)
    {
        spellSlot.button.Set(components.Visibility.Visible);
        spellSlot.button.Set(new components.ui.TooltipUID(UIDSpellSlotNames[spellSlot.index]));
        if (spellSlot.summary != default)
            spellSlot.summary.Set(new components.ui.Label(""));
        if (spellSlot.req != default)
            spellSlot.req.Set(components.Visibility.Visible);
    }

    private void CreateSpellSummary(DefaultEcs.Entity entity)
    {
        ref var spellSlot = ref entity.Get<components.ui.SpellSlot>();
        if (spellSlot.summary != default) spellSlot.summary.Dispose();
        spellSlot.summary = preload.CreateLabel(entity)
            .With(spellSlot.button.Get<Rect>().Min + new Vector2(0, 44))
            .With(UIPreloadAsset.Fnt002)
            .Build();
    }

    private void CreateSpellReq(DefaultEcs.Entity entity)
    {
        ref var spellSlot = ref entity.Get<components.ui.SpellSlot>();
        var spellReq = ((InventoryFairy)(entity.Get<components.Parent>().Entity.Get<components.ui.Slot>().card!)).spellReqs[spellSlot.index];
        var isAttack = spellSlot.index % 2 == 0;
        var pos = spellSlot.button.Get<Rect>().Min + new Vector2(2, 45);

        if (spellSlot.req != default) spellSlot.req.Dispose();
        spellSlot.req = World.CreateEntity();
        spellSlot.req.Set(new components.Parent(entity));
        spellSlot.req.Set(components.Visibility.Visible);
        spellSlot.req.Set(components.ui.UIOffset.Center);
        spellSlot.req.Set(new components.ui.RenderOrder(0));
        spellSlot.req.Set(IColor.White);
        assetRegistry.LoadUITileSheet(spellSlot.req, isAttack ? UIPreloadAsset.Cls001 : UIPreloadAsset.Cls000);

        var tileSize = spellSlot.req.Get<rendering.TileSheet>().GetPixelSize(0);
        spellSlot.req.Set(Rect.FromTopLeftSize(pos, tileSize * 3));
        spellSlot.req.Set(spellReq.Select((req, i) => new components.ui.Tile(
            TileId: (int)req,
            Rect: Rect.FromTopLeftSize(pos + i * new Vector2(8, 5), tileSize)))
            .ToArray());
    }

    private string FormatManaAmount(InventorySpell spell)
    {
        var dbSpell = mappedDB.GetSpell(spell.dbUID);
        return dbSpell.MaxMana == 5
            ? "{104}-"
            : $"{{104}}{spell.mana}/{dbSpell.MaxMana}";
    }
}
