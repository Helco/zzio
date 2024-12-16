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

    private DefaultEcs.Entity CreateSpellSlot(DefaultEcs.Entity parent, ref components.ui.Card card, int index)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));
        entity.Set(new components.ui.SpellSlot());
        ref var spell = ref entity.Get<components.ui.SpellSlot>();
        spell.index = index;

        spell.button = preload.CreateButton(entity)
            .With(card.buttonId + index + 1)
            .With(card.button.Get<Rect>().Min + new Vector2(81 + 46 * spell.index))
            .With(new components.ui.ButtonTiles(-1))
            .With(UIPreloadAsset.Spl000)
            // .WithTooltip(UIDSpellSlotNames[i])
            .Build();

        InfoMode(ref spell);

        return entity;
    }

    public void SetSpell(DefaultEcs.Entity entity, ref components.ui.SpellSlot spell, InventorySpell invSpell)
    {
        spell.spell = invSpell;
        spell.button.Set(new components.ui.ButtonTiles(invSpell.cardId.EntityId));
        if (spell.summary != default) spell.summary.Dispose();
        spell.summary = preload.CreateLabel(entity)
            .With(spell.button.Get<Rect>().Min + new Vector2(0, 44))
            .With(UIPreloadAsset.Fnt002)
            .WithText(FormatManaAmount(spell.spell))
            .Build();
        if (spell.req != default) spell.req.Dispose();
        spell.req = CreateSpellReq(
            entity,
            ((InventoryFairy)(entity.Get<components.Parent>().Entity.Get<components.ui.Card>().card!)).spellReqs[spell.index],
            (spell.index % 2) == 0,
            spell.button.Get<Rect>().Min + new Vector2(2, 45)
        );
    }

    public static void InfoMode(ref components.ui.SpellSlot spell)
    {
        spell.button.Set(components.Visibility.Invisible);
        spell.button.Set(new components.ui.TooltipUID(UIDFairyInfoDescriptions[spell.index]));
        if (spell.summary != default)
            spell.summary.Set(components.Visibility.Visible);
        if (spell.req != default)
            spell.req.Set(components.Visibility.Invisible);
    }

    public static void SpellMode(ref components.ui.SpellSlot spell)
    {
        spell.button.Set(components.Visibility.Visible);
        spell.button.Set(new components.ui.TooltipUID(UIDSpellSlotNames[spell.index]));
        if (spell.summary != default)
            spell.summary.Set(components.Visibility.Invisible);
        if (spell.req != default)
            spell.req.Set(components.Visibility.Visible);
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
