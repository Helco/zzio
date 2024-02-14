using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.Resource;
using zzio.db;
using zzre.game.resources;
using KeyCode = Silk.NET.SDL.KeyCode;
using static zzre.game.systems.ui.InGameScreen;

namespace zzre.game.systems.ui;

public partial class ScrMapMenu : BaseScreen<components.ui.ScrMapMenu, messages.ui.OpenMapMenu>
{
    public ScrMapMenu(ITagContainer diContainer) : base(diContainer, BlockFlags.All)
    {
        OnElementDown += HandleElementDown;
    }

    protected override void HandleOpen(in messages.ui.OpenMapMenu message)
    {
        var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
        if (!inventory.Contains(StdItemId.MapFairyGarden))
            return;

        var entity = World.CreateEntity();
        entity.Set<components.ui.ScrMapMenu>();
        ref var mapMenu = ref entity.Get<components.ui.ScrMapMenu>();
        mapMenu.Inventory = inventory;

        preload.CreateFullBackOverlay(entity);

        var mapEntity = preload.CreateImage(entity)
            .With(new Rect(-320, -240, 640, 480))
            .WithRenderOrder(1)
            .Build();
        mapEntity.Set(ManagedResource<materials.UIMaterial>.Create(
            new RawMaskedBitmapInfo(colorFile: "map001t.bmp", maskFile: "map001m.raw")));
        var mapMaterial = mapEntity.Get<materials.UIMaterial>();
        mapMaterial.MaskBits.Value = CollectMaskBits(inventory);

        preload.CreateTooltipTarget(entity)
            .With(new Vector2(-320 + 11, -240 + 11))
            .WithText("{205} - ")
            .Build();

        CreateTopButtons(preload, entity, inventory, IDOpenMap);
    }

    private static readonly IReadOnlyList<StdItemId> MapSectionItems = new[]
    {
        (StdItemId)(ushort.MaxValue),
        StdItemId.MapShadowRealm,
        StdItemId.MapFairyGarden,
        (StdItemId)(ushort.MaxValue),
        StdItemId.MapSkyRealm,
        StdItemId.MapDarkSwamp,
        StdItemId.MapForest,
        StdItemId.MapMountain
    };
    private static uint CollectMaskBits(Inventory inventory) => MapSectionItems
        .Select((item, i) => inventory.Contains(item) ? 1u << i : 0u)
        .Aggregate((a, b) => a | b);

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.ui.ScrMapMenu mapMenu)
    {
        base.Update(timeElapsed, entity, ref mapMenu);
    }

    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId id)
    {
        var mapMenuEntity = Set.GetEntities()[0];
        if (id == IDOpenDeck)
        {
            mapMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenDeck>();
        }
        else if (id == IDOpenRunes)
        {
            mapMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenRuneMenu>();
        }
        else if (id == IDOpenFairybook)
        {
            mapMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenBookMenu>();
        }
        else if (id == IDClose)
            mapMenuEntity.Dispose();
    }

    protected override void HandleKeyDown(KeyCode key)
    {
        var mapMenuEntity = Set.GetEntities()[0];
        base.HandleKeyDown(key);
        if (key == KeyCode.KF2) {
            mapMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenRuneMenu>();
        }
        if (key == KeyCode.KF3) {
            mapMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenBookMenu>();
        }
        if (key == KeyCode.KF5) {
            mapMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenDeck>();
        }
        if (key == KeyCode.KReturn || key == KeyCode.KEscape || key == KeyCode.KF3)
            Set.DisposeAll();
    }
}
