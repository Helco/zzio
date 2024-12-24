using System;
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
    private static readonly UID UIDEvol = new(0x69226721); // Evolution at level

    // elementIds: 0-76 are fairy buttons
    // 1000 etc are nav buttons from InGameScreen

    public ScrBookMenu(ITagContainer diContainer) : base(diContainer, BlockFlags.All)
    {
        db = diContainer.GetTag<MappedDB>();
        OnElementDown += HandleElementDown;
    }

    protected override void HandleOpen(in messages.ui.OpenBookMenu message)
    {
        World.Publish(new messages.SpawnSample($"resources/audio/sfx/gui/_g006.wav"));
        var uiEntity = World.CreateEntity();
        uiEntity.Set<components.ui.ScrBookMenu>();
        ref var book = ref uiEntity.Get<components.ui.ScrBookMenu>();
        book.Fairies = [.. db.Fairies.OrderBy(fairyRow => fairyRow.CardId.EntityId)];
        book.Sidebar = default;
        book.Crosshair = default;

        preload.CreateFullBackOverlay(uiEntity);

        // Draw Fairy Book background
        preload.CreateImage(uiEntity)
            .With(components.ui.FullAlignment.Center)
            .WithBitmap("col000")
            .WithRenderOrder(1)
            .Build();

        preload.CreateTooltipTarget(uiEntity)
            .With(Mid + new Vector2(11, 11))
            .WithText("{205} - ")
            .Build();

        CreateTopButtons(preload, uiEntity, inventory, IDOpenFairybook);
        CreateFairyButtons(uiEntity, ref book);
    }

    private void CreateFairyButtons(in DefaultEcs.Entity entity, ref components.ui.ScrBookMenu book)
    {
        var fairies = book.Fairies;
        for (int i = 0; i < fairies.Length; i++)
        {
            if (inventory.Contains(fairies[i].CardId))
            {
                // Fairy icon
                var button = preload.CreateButton(entity)
                    .With(new components.ui.ElementId(i))
                    .With(Mid + FairyButtonPos(i))
                    .With(new components.ui.ButtonTiles(fairies[i].CardId.EntityId))
                    .With(UIPreloadAsset.Wiz000)
                    .Build();
                button.Set(button.Get<Rect>().GrownBy(new Vector2(5, 5))); // No gaps
                button.Set(new components.ui.Silent());

                // In the original engine, only the first fairy is checked for isInUse
                // This is an intentional bug fix
                if (inventory.Fairies.Any(c => fairies[i].CardId == c.cardId && c.isInUse))
                {
                    // "Fairy is equipped" indicator
                    preload.CreateImage(entity)
                        .With(Mid + FairyButtonPos(i))
                        .With(UIPreloadAsset.Inf000, 16)
                        .WithRenderOrder(-1)
                        .Build();
                }
            }
        }
    }

    private DefaultEcs.Entity CreateSidebar(in DefaultEcs.Entity parent, ref components.ui.ScrBookMenu book, int fairyI)
    {
        var entity = World.CreateEntity();
        entity.Set(new components.Parent(parent));

        var fairyRow = book.Fairies[fairyI];

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(21, 57))
            .WithText($"#{fairyI + 1} {fairyRow.Name}")
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

        preload.CreateImage(entity)
            .With(Mid + new Vector2(160, 218))
            .With(UIPreloadAsset.Wiz000, fairyRow.CardId.EntityId)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(21, 271))
            .WithText(String.Join("\n", UIDStatNames.Select(uid => db.GetText(uid).Text)))
            .WithLineHeight(17)
            .With(UIPreloadAsset.Fnt002)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(111, 266))
            .WithText(StatsLights([
                Math.Min(500, fairyRow.MHP) / 100,
                fairyRow.MovSpeed + 1,
                fairyRow.JumpPower + 1,
                fairyRow.CriticalHit + 1
            ]))
            .WithLineHeight(17)
            .With(UIPreloadAsset.Fnt001)
            .Build();

        preload.CreateLabel(entity)
            .With(Mid + new Vector2(21, 346))
            .WithText(fairyRow.Info)
            .With(UIPreloadAsset.Fnt002)
            .WithLineWrap(190f)
            .Build();

        return entity;
    }

    private static string StatsLights(int[] values) =>
        String.Join("\n", values.Select(value => UIBuilder.GetLightsIndicator(value)));

    private static Vector2 FairyButtonPos(int fairyI) =>
        new Vector2(226 + 45 * (fairyI % 9), 66 + 45 * (fairyI / 9));

    private void HandleElementDown(DefaultEcs.Entity clickedEntity, components.ui.ElementId id)
    {
        var uiEntity = Set.GetEntities()[0];
        ref var book = ref uiEntity.Get<components.ui.ScrBookMenu>();
        var fairyI = id.Value;
        var fairyRow = book.Fairies.ElementAtOrDefault(fairyI);
        if (fairyRow != default)
        {
            book.Sidebar.Dispose();
            book.Sidebar = CreateSidebar(uiEntity, ref book, fairyI);
            book.Crosshair.Dispose();
            book.Crosshair = preload.CreateImage(uiEntity)
                .With(Mid + new Vector2(-2, -2) + FairyButtonPos(fairyI))
                .With(UIPreloadAsset.Dnd000, 0)
                .WithRenderOrder(-2)
                .Build();
        }
        HandleNavClick(id, zanzarah, uiEntity, IDOpenFairybook);
    }

    protected override void HandleKeyDown(KeyCode key)
    {
        var uiEntity = Set.GetEntities()[0];
        base.HandleKeyDown(key);
        HandleNavKeyDown(key, zanzarah, uiEntity, IDOpenFairybook);
    }
}
