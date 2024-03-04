﻿using System;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.db;
using KeyCode = Silk.NET.SDL.KeyCode;
using static zzre.game.systems.ui.InGameScreen;

namespace zzre.game.systems.ui;

public partial class ScrBookMenu : BaseScreen<components.ui.ScrBookMenu, messages.ui.OpenBookMenu>
{
    private readonly MappedDB db;

    private static readonly UID[] UIDStatNames =
    [
        new(0x3D26ACB1), // Hit Points
        new(0xAB46B8B1), // Dexterity
        new(0xB031B8B1), // Jump Ability
        new(0xB6CA5A11)  // Special
    ];
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

        book.Fairies = [.. db.Fairies.OrderBy(fairyRow => fairyRow.CardId.EntityId)];
        book.FairyButtons = [];
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

    private static void CreateFairyButtons(UIBuilder preload, in DefaultEcs.Entity entity, Inventory inventory, ref components.ui.ScrBookMenu book)
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
                    .With(UIPreloadAsset.Wiz000)
                    .Build();
                button.Set(button.Get<Rect>().GrownBy(new Vector2(5, 5))); // No gaps
                button.Set(new components.ui.Silent());
                book.FairyButtons.Add(element, fairies[i]);

                // In the original engine, only the first fairy is checked for isInUse
                // This is an intentional bug fix
                if (inventory.Fairies.Any(c => fairies[i].CardId == c.cardId && c.isInUse)) {
                    preload.CreateImage(entity)
                        .With(Mid + FairyButtonPos(i))
                        .With(UIPreloadAsset.Inf000, 16)
                        .WithRenderOrder(-1)
                        .Build();
                }
            }
        }
    }

    private DefaultEcs.Entity CreateSidebar(UIBuilder preload, in DefaultEcs.Entity parent, FairyRow fairyRow, ref components.ui.ScrBookMenu book)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        var fairyI = Array.IndexOf(book.Fairies, fairyRow) + 1;

        var element = new components.ui.ElementId(0);
        preload.CreateButton(entity)
            .With(element)
            .With(Mid + new Vector2(160, 218))
            .With(new components.ui.ButtonTiles(fairyRow.CardId.EntityId))
            .With(UIPreloadAsset.Wiz000)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(21, 57))
            .WithText($"#{fairyI} {fairyRow.Name}")
            .With(UIPreloadAsset.Fnt000)
            .Build();

        preload.CreateImage(entity)
            .With(Mid + new Vector2(22, 81))
            .With(UIPreloadAsset.Cls000, (int)fairyRow.Class0)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(36, 80))
            .WithText(preload.GetClassText(fairyRow.Class0))
            .With(UIPreloadAsset.Fnt002)
            .Build();

        if (fairyRow.EvolVar != -1)
            preload.CreateLabel(entity)
                .With(Mid + new Vector2(22, 246))
                .WithText($"{db.GetText(UIDEvol).Text} {fairyRow.EvolVar}")
                .With(UIPreloadAsset.Fnt002)
                .Build();

        CreateStat(preload, entity, 0, Math.Min(500, fairyRow.MHP) / 100);
        CreateStat(preload, entity, 1, fairyRow.MovSpeed + 1);
        CreateStat(preload, entity, 2, fairyRow.JumpPower + 1);
        CreateStat(preload, entity, 3, fairyRow.CriticalHit + 1);

        const float MaxTextWidth = 190f;
        preload.CreateLabel(entity)
            .With(Mid + new Vector2(21, 346))
            .WithText(fairyRow.Info)
            .With(UIPreloadAsset.Fnt002)
            .WithLineWrap(MaxTextWidth)
            .Build();

        return entity;
    }

    private void CreateStat(UIBuilder preload, in DefaultEcs.Entity entity, int index, int value)
    {
        preload.CreateLabel(entity)
            .With(Mid + new Vector2(21, 271 + index*17))
            .WithText(db.GetText(UIDStatNames[index]).Text)
            .With(UIPreloadAsset.Fnt002)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(111, 266 + index*17))
            .WithText(UIBuilder.GetLightsIndicator(value))
            .With(UIPreloadAsset.Fnt001)
            .Build();
    }

    private static Vector2 FairyButtonPos(int fairyI) {
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
                .With(UIPreloadAsset.Dnd000, 0)
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

    protected override void HandleKeyDown(KeyCode key)
    {
        var bookMenuEntity = Set.GetEntities()[0];
        base.HandleKeyDown(key);
        if (key == KeyCode.KF2) {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenRuneMenu>();
        }
        if (key == KeyCode.KF4) {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenMapMenu>();
        }
        if (key == KeyCode.KF5) {
            bookMenuEntity.Dispose();
            zanzarah.UI.Publish<messages.ui.OpenDeck>();
        }
        if (key == KeyCode.KReturn|| key == KeyCode.KEscape || key == KeyCode.KF3)
            Set.DisposeAll();
    }
}
