using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using KeyCode = Silk.NET.SDL.KeyCode;
using static zzre.game.systems.ui.InGameScreen;

namespace zzre.game.systems.ui;

public partial class ScrMapMenu : BaseScreen<components.ui.ScrMapMenu, messages.ui.OpenMapMenu>
{
    private readonly IAssetRegistry assetRegistry;

    public ScrMapMenu(ITagContainer diContainer) : base(diContainer, BlockFlags.All)
    {
        assetRegistry = diContainer.GetTag<IAssetRegistry>();
        OnElementDown += HandleElementDown;
    }

    protected override void HandleOpen(in messages.ui.OpenMapMenu message)
    {
        var entity = World.CreateEntity();
        entity.Set<components.ui.ScrMapMenu>();
        ref var mapMenu = ref entity.Get<components.ui.ScrMapMenu>();
        mapMenu.Inventory = inventory;

        preload.CreateFullBackOverlay(entity);

        var mapEntity = preload.CreateImage(entity)
            .With(new Rect(-320, -240, 640, 480))
            .WithRenderOrder(1)
            .Build();
        var mapHandle = assetRegistry.LoadUIBitmap(mapEntity, "map001", hasRawMask: true);
        mapHandle.Get().Material.MaskBits.Value = CollectMaskBits(inventory);

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
        HandleNavClick(id, zanzarah, mapMenuEntity, IDOpenMap);
    }

    protected override void HandleKeyDown(KeyCode key)
    {
        var mapMenuEntity = Set.GetEntities()[0];
        base.HandleKeyDown(key);
        HandleNavKeyDown(key, zanzarah, mapMenuEntity, IDOpenMap);
    }
}
