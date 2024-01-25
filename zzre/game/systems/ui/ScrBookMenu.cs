using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.db;
using static zzre.game.systems.ui.InGameScreen;

namespace zzre.game.systems.ui;

public partial class ScrBookMenu : BaseScreen<components.ui.ScrBookMenu, messages.ui.OpenBookMenu>
{
    private readonly MappedDB db;

    public ScrBookMenu(ITagContainer diContainer) : base(diContainer, BlockFlags.All)
    {
        db = diContainer.GetTag<MappedDB>();
        OnElementDown += HandleElementDown;
    }

    protected override void HandleOpen(in messages.ui.OpenBookMenu message)
    {
        var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
        if (!inventory.Contains(StdItemId.FairyBook))
            return;

        var entity = World.CreateEntity();
        entity.Set<components.ui.ScrBookMenu>();
        ref var bookMenu = ref entity.Get<components.ui.ScrBookMenu>();
        bookMenu.Inventory = inventory;

        preload.CreateImage(entity)
            .With(-new Vector2(320, 240))
            .WithBitmap("col000")
            .WithRenderOrder(1)
            .Build();

        preload.CreateTooltipTarget(entity)
            .With(new Vector2(-320 + 11, -240 + 11))
            .WithText("{205} - ")
            .Build();

        CreateTopButtons(preload, entity, inventory, IDOpenFairybook);
        CreateFairyButtons(preload, entity, inventory);
    }

    private void CreateFairyButtons(UIPreloader preload, in DefaultEcs.Entity entity, Inventory inventory)
    {
        var fairies = db.Fairies.OrderBy(fairyRow => fairyRow.CardId.EntityId).ToArray();
        for (int i = 0; i < fairies.Length; i++)
        {
            var fairyRow = fairies[i];
            foreach (var ownedFairy in inventory.Fairies)
            {
                if (ownedFairy.cardId == fairyRow.CardId)
                {
                    var element = new components.ui.ElementId(1 + i);
                    preload.CreateButton(entity)
                        .With(element)
                        .With(Mid + new Vector2(226 + 45 * (i % 9), 66 + 45 * (i / 9)))
                        .With(new components.ui.ButtonTiles(ownedFairy.cardId.EntityId))
                        .With(preload.Wiz000)
                        .Build();
                    break;
                }
            }
        }
    }

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.ui.ScrBookMenu bookMenu)
    {
        base.Update(timeElapsed, entity, ref bookMenu);
    }

    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId id)
    {
        var bookMenuEntity = Set.GetEntities()[0];
        if (id == IDOpenDeck)
        {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenDeck>();
        }
        else if (id == IDOpenRunes)
        {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenRuneMenu>();
        }
        else if (id == IDOpenMap)
        {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenMapMenu>();
        }
        else if (id == IDClose)
            bookMenuEntity.Dispose();
    }

    protected override void HandleKeyDown(Key key)
    {
        var bookMenuEntity = Set.GetEntities()[0];
        base.HandleKeyDown(key);
        if (key == Key.F2) {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenRuneMenu>();
        }
        if (key == Key.F4) {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenMapMenu>();
        }
        if (key == Key.F5) {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenDeck>();
        }
        if (key == Key.Enter || key == Key.Escape || key == Key.F3)
            Set.DisposeAll();
    }
}
