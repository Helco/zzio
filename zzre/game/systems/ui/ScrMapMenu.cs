using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.db;
using static zzre.game.systems.ui.InGameScreen;

namespace zzre.game.systems.ui;

public partial class ScrMapMenu : BaseScreen<components.ui.ScrMapMenu, messages.ui.OpenMapMenu>
{
    private readonly MappedDB db;

    public ScrMapMenu(ITagContainer diContainer) : base(diContainer, BlockFlags.All)
    {
        db = diContainer.GetTag<MappedDB>();
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

        preload.CreateImage(entity)
            .With(-new Vector2(320, 240))
            .WithBitmap("map001")
            .WithRenderOrder(1)
            .Build();

        preload.CreateTooltipTarget(entity)
            .With(new Vector2(-320 + 11, -240 + 11))
            .WithText("{205} - ")
            .Build();

        CreateTopButtons(preload, entity, inventory, IDOpenMap);
    }

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

    protected override void HandleKeyDown(Key key)
    {
        var mapMenuEntity = Set.GetEntities()[0];
        base.HandleKeyDown(key);
        if (key == Key.F2) {
            mapMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenRuneMenu>();
        }
        if (key == Key.F3) {
            mapMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenBookMenu>();
        }
        if (key == Key.F5) {
            mapMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenDeck>();
        }
        if (key == Key.Enter || key == Key.Escape || key == Key.F4)
            Set.DisposeAll();
    }
}
