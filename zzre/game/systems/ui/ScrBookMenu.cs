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

    private static readonly UID[] UIDStatNames = new UID[]
    {
        new(0x3D26ACB1), // Hit Points
        new(0xAB46B8B1), // Dexterity
        new(0xB031B8B1), // Jump Ability
        new(0xB6CA5A11)  // Special
    };
    private static readonly UID UIDEvol = new (0x69226721); // Evolution at level

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
        ref var book = ref entity.Get<components.ui.ScrBookMenu>();
        book.Inventory = inventory;

        preload.CreateFullBackOverlay(entity);

        book.Fairies = db.Fairies.OrderBy(fairyRow => fairyRow.CardId.EntityId).ToArray();
        book.FairyButtons = new Dictionary<components.ui.ElementId, FairyRow>();
        book.Sidebar = default;
        book.Crosshair = default;

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
        CreateFairyButtons(preload, entity, inventory, ref book);
    }

    private void CreateFairyButtons(UIPreloader preload, in DefaultEcs.Entity entity, Inventory inventory, ref components.ui.ScrBookMenu book)
    {
        var fairies = book.Fairies;
        for (int i = 0; i < fairies.Length; i++)
        {
            if (inventory.Contains(fairies[i].CardId))
            {
                var element = new components.ui.ElementId(1 + i);
                var button = preload.CreateButton(entity)
                    .With(element)
                    .With(Mid + FairyButtonPos(i))
                    .With(new components.ui.ButtonTiles(fairies[i].CardId.EntityId))
                    .With(preload.Wiz000)
                    .Build();
                book.FairyButtons.Add(element, fairies[i]);

                // In the original engine, only the first fairy is checked for isInUse
                // This is an intentional bug fix
                if (inventory.Fairies.Any(c => fairies[i].CardId == c.cardId && c.isInUse)) {
                    preload.CreateImage(entity)
                        .With(Mid + FairyButtonPos(i))
                        .With(preload.Inf000, 16)
                        .WithRenderOrder(-1)
                        .Build();
                }
            }
        }
    }

    private DefaultEcs.Entity CreateSidebar(UIPreloader preload, in DefaultEcs.Entity parent, FairyRow fairyRow, ref components.ui.ScrBookMenu book)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        var fairyI = Array.IndexOf(book.Fairies, fairyRow) + 1;

        var element = new components.ui.ElementId(0);
        preload.CreateButton(entity)
            .With(element)
            .With(Mid + new Vector2(160, 218))
            .With(new components.ui.ButtonTiles(fairyRow.CardId.EntityId))
            .With(preload.Wiz000)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(21, 57))
            .WithText($"#{fairyI} {fairyRow.Name}")
            .With(preload.Fnt000)
            .Build();

        preload.CreateImage(entity)
            .With(Mid + new Vector2(22, 81))
            .With(preload.Cls000, (int)fairyRow.Class0)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(36, 80))
            .WithText(preload.GetClassText((int)fairyRow.Class0))
            .With(preload.Fnt002)
            .Build();

        if (fairyRow.EvolVar != -1)
            preload.CreateLabel(entity)
                .With(Mid + new Vector2(22, 246))
                .WithText($"{db.GetText(UIDEvol).Text} {fairyRow.EvolVar}")
                .With(preload.Fnt002)
                .Build();

        CreateStat(preload, entity, 0, Math.Min(500, fairyRow.MHP) / 100);
        CreateStat(preload, entity, 1, fairyRow.MovSpeed + 1);
        CreateStat(preload, entity, 2, fairyRow.JumpPower + 1);
        CreateStat(preload, entity, 3, fairyRow.CriticalHit + 1);

        const float MaxTextWidth = 190f;
        preload.CreateLabel(entity)
            .With(Mid + new Vector2(21, 346))
            .WithText(fairyRow.Info)
            .With(preload.Fnt002)
            .WithLineWrap(MaxTextWidth)
            .Build();

        return entity;
    }

    private void CreateStat(UIPreloader preload, in DefaultEcs.Entity entity, int index, int value)
    {
        preload.CreateLabel(entity)
            .With(Mid + new Vector2(21, 271 + index*17))
            .WithText(db.GetText(UIDStatNames[index]).Text)
            .With(preload.Fnt002)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(111, 266 + index*17))
            .WithText(preload.GetLightsIndicator(value))
            .With(preload.Fnt001)
            .Build();
    }

    private Vector2 FairyButtonPos(int fairyI) {
        return new Vector2(226 + 45 * (fairyI % 9), 66 + 45 * (fairyI / 9));
    }

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.ui.ScrBookMenu bookMenu)
    {
        base.Update(timeElapsed, entity, ref bookMenu);
    }

    private void HandleElementDown(DefaultEcs.Entity entity, components.ui.ElementId id)
    {
        var bookMenuEntity = Set.GetEntities()[0];
        ref var book = ref bookMenuEntity.Get<components.ui.ScrBookMenu>();

        if (book.FairyButtons.TryGetValue(id, out var fairyRow))
        {
            book.Sidebar.Dispose();
            book.Sidebar = CreateSidebar(preload, entity, fairyRow, ref book);
            book.Crosshair.Dispose();
            book.Crosshair = preload.CreateImage(entity)
                .With(Mid + new Vector2(-2, -2) + FairyButtonPos(book.Fairies.IndexOf(fairyRow)))
                .With(preload.Dnd000, 0)
                .WithRenderOrder(-2)
                .Build();
        }

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
