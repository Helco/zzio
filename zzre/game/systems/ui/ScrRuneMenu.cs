using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.db;
using static zzre.game.systems.ui.InGameScreen;

namespace zzre.game.systems.ui;

public partial class ScrRuneMenu : BaseScreen<components.ui.ScrRuneMenu, messages.ui.OpenRuneMenu>
{
    private readonly record struct RuneInfo(
        float x, float y,
        bool isRight,
        int scene,
        StdItemId item,
        UID nameUID,
        components.ui.ElementId elementId);

    private static readonly components.ui.ElementId IDCaves = new(1);
    private static readonly components.ui.ElementId IDMountain = new(2);
    private static readonly components.ui.ElementId IDLondon = new(3);
    private static readonly components.ui.ElementId IDFairyGarden = new(4);
    private static readonly components.ui.ElementId IDEnchantedForest = new(5);
    private static readonly components.ui.ElementId IDSwamp = new(6);
    private static readonly components.ui.ElementId IDCottage = new(7);
    private static readonly components.ui.ElementId IDSkyIsles = new(8);
    private static readonly components.ui.ElementId IDDarkzone = new(9);
    private static readonly components.ui.ElementId IDTopOfWorld = new(10);

    private static readonly IReadOnlyList<RuneInfo> RuneInfos = new RuneInfo[]
    {
        new(179f, 97f, true, 2211, StdItemId.RuneCaves, new(0xDE313331), IDCaves),
        new(421f, 97f, false, 2610, StdItemId.RuneMountain, new(0xD5934331), IDMountain),
        new(151f, 157f, true, 2801, StdItemId.RuneLondon, new(0xB4C4731), IDLondon),
        new(449f, 157f, false, 2421, StdItemId.RuneFairyGarden, new(0x3FC44B31), IDFairyGarden),
        new(139f, 218f, true, 232, StdItemId.RuneEnchantedForest, new(0x33FB4331), IDEnchantedForest),
        new(461f, 218f, false, 1243, StdItemId.RuneSwamp, new(0xD2A84331), IDSwamp),
        new(151f, 280f, true, 2420, StdItemId.RuneCottage, new(0xA0E84731), IDCottage),
        new(449f, 280f, false, 1031, StdItemId.RuneSkyIsles, new(0xD7334B31), IDSkyIsles),
        new(178f, 340f, true, 830, StdItemId.RuneDarkzone, new(0xF2454F31), IDDarkzone),
        new(421f, 340f, false, 1623, StdItemId.RuneTopOfWorld, new(0x1B144F31), IDTopOfWorld)
    };

    private readonly MappedDB db;

    public ScrRuneMenu(ITagContainer diContainer) : base(diContainer, BlockFlags.All)
    {
        db = diContainer.GetTag<MappedDB>();
        OnElementDown += HandleElementDown;
    }

    protected override void HandleOpen(in messages.ui.OpenRuneMenu message)
    {
        var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
        if (!inventory.Contains(StdItemId.RuneFairyGarden))
            return;

        var entity = World.CreateEntity();
        entity.Set<components.ui.ScrRuneMenu>();
        ref var runeMenu = ref entity.Get<components.ui.ScrRuneMenu>();
        runeMenu.Inventory = inventory;

        preload.CreateImage(entity)
            .With(-new Vector2(320, 240))
            .WithBitmap("mnu000")
            .WithRenderOrder(1)
            .Build();

        preload.CreateTooltipTarget(entity)
            .With(new Vector2(-320 + 11, -240 + 11))
            .WithText("{205} - ")
            .Build();

        CreateTopButtons(preload, entity, inventory, IDOpenRunes);

        var runeButtons = new List<DefaultEcs.Entity>(RuneInfos.Count);
        foreach (var info in RuneInfos)
        {
            if (!inventory.Contains(info.item))
                continue;
            var name = db.GetText(info.nameUID).Text;
            var text = info.isRight ? $"{name} {{3{(int)info.item}}}" : $"{{3{(int)info.item}}} {name}";
            var pos = new Vector2(-320 + info.x, -240 + 16 + info.y);
            if (info.isRight)
                pos.X += 39;
            var runeButton = preload.CreateLabel(entity)
                .With(pos)
                .With(info.isRight
                    ? components.ui.FullAlignment.CenterRight
                    : components.ui.FullAlignment.CenterLeft)
                .WithText(text)
                .With(preload.Fnt000)
                .Build();
            runeButton.Set(info.elementId);

            var tileSheet = runeButton.Get<rendering.TileSheet>();
            var size = new Vector2(tileSheet.GetUnformattedWidth(text), tileSheet.GetTextHeight(text));
            pos = runeButton.Get<Rect>().Min; // it was modified during build
            runeButton.Set(Rect.FromTopLeftSize(pos, size));

            runeButtons.Add(runeButton);
        }
        runeMenu.RuneButtons = runeButtons;
    }

    private const string HoveredFontPrefix = "{5*"; // fnt000 to fnt003
    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.ui.ScrRuneMenu runeMenu)
    {
        base.Update(timeElapsed, entity, ref runeMenu);

        var curHovered = World.Has<components.ui.HoveredElement>()
            ? World.Get<components.ui.HoveredElement>()
            : default;
        if (curHovered.Entity == runeMenu.LastHoveredRune)
            return;

        if (runeMenu.LastHoveredRune != default)
        {
            ref var oldLabel = ref runeMenu.LastHoveredRune.Get<components.ui.Label>();
            oldLabel.Text = oldLabel.Text[HoveredFontPrefix.Length..];
            runeMenu.LastHoveredRune.Set(oldLabel); // necessary to update visuals
            runeMenu.LastHoveredRune = default;
        }
        if (runeMenu.RuneButtons.Contains(curHovered.Entity))
        {
            ref var newLabel = ref curHovered.Entity.Get<components.ui.Label>();
            newLabel.Text = HoveredFontPrefix + newLabel.Text;
            curHovered.Entity.Set(newLabel);
            runeMenu.LastHoveredRune = curHovered.Entity;
        }
    }

    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId id)
    {
        var runeMenuEntity = Set.GetEntities()[0];
        var runeInfo = RuneInfos.FirstOrDefault(i => i.elementId == id);

        if (id == runeInfo.elementId)
        {
            runeMenuEntity.Dispose();
            zanzarah.CurrentGame!.Publish(new messages.Teleport(runeInfo.scene, targetEntry: -1));
        }
        else if (id == IDOpenDeck)
        {
            runeMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenDeck>();
        }
        else if (id == IDOpenFairybook)
        {
            runeMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenBookMenu>();
        }
        else if (id == IDOpenMap)
        {
            runeMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenMapMenu>();
        }
        else if (id == IDClose)
            runeMenuEntity.Dispose();
    }

    protected override void HandleKeyDown(Key key)
    {
        var runeMenuEntity = Set.GetEntities()[0];
        base.HandleKeyDown(key);
        if (key == Key.F3) {
            runeMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenBookMenu>();
        }
        if (key == Key.F4) {
            runeMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenMapMenu>();
        }
        if (key == Key.F5) {
            runeMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenDeck>();
        }
        if (key == Key.Enter || key == Key.Escape || key == Key.F2)
            Set.DisposeAll();
    }
}
