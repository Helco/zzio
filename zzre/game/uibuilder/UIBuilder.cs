using System;
using System.Linq;
using System.Numerics;
using zzio;
using zzio.db;

namespace zzre.game;

public class UIBuilder : BaseDisposable
{
    public static readonly FColor DefaultOverlayColor = new(0.029999999f, 0.050000001f, 0.029999999f, 0.8f);

    internal readonly DefaultEcs.World UIWorld;
    internal readonly UI UI;
    private readonly AssetHandle<UIPreloadAsset> preloadAssetHandle;
    private readonly MappedDB mappedDb;
    private readonly IAssetRegistry assetRegistry;

    private static readonly UID UIDYouHave = new(0x070EE421);

    public UIBuilder(ITagContainer diContainer)
    {
        UIWorld = diContainer.GetTag<DefaultEcs.World>();
        UI = diContainer.GetTag<UI>();
        mappedDb = diContainer.GetTag<MappedDB>();
        assetRegistry = diContainer.GetTag<IAssetRegistry>();

        preloadAssetHandle = assetRegistry.Load(new UIPreloadAsset.Info(), AssetLoadPriority.High).As<UIPreloadAsset>();
    }

    protected override void DisposeManaged()
    {
        preloadAssetHandle.Dispose();
    }

    public static UITileSheetAsset.Info GetTileSheetByCardType(CardType type) => type switch
    {
        CardType.Fairy => UIPreloadAsset.Wiz000,
        CardType.Item => UIPreloadAsset.Itm000,
        CardType.Spell => UIPreloadAsset.Spl000,
        _ => throw new NotSupportedException($"Unsupported card type {type}")
    };

    internal string GetDBText(UID textUID) => textUID.Module switch
    {
        (int)ModuleType.Text => mappedDb.GetText(textUID).Text,
        (int)ModuleType.Dialog => mappedDb.GetDialog(textUID).Text,
        _ => throw new ArgumentException("Invalid UID for UI")
    };

    internal uibuilder.Label CreateLabel(DefaultEcs.Entity parent) => new(this, parent);

    internal uibuilder.TooltipArea CreateTooltipArea(DefaultEcs.Entity parent) => new(this, parent);

    internal uibuilder.TooltipTarget CreateTooltipTarget(DefaultEcs.Entity parent) => new(this, parent);

    internal uibuilder.Button CreateButton(DefaultEcs.Entity parent) => new(this, parent);

    internal uibuilder.Image CreateImage(DefaultEcs.Entity parent) => new(this, parent);

    public void CreateDialogBackground(
        DefaultEcs.Entity parent,
        bool animateOverlay,
        out Rect backgroundRect,
        float opacity = 0.8f)
    {
        var image = CreateImage(parent)
            .WithBitmap("std000")
            .With(components.ui.FullAlignment.Center)
            .WithRenderOrder(1)
            .Build();
        backgroundRect = image.Get<Rect>();

        CreateBackOverlay(parent, animateOverlay, opacity, backgroundRect);
    }

    public void CreateBackOverlay(
        DefaultEcs.Entity parent,
        bool animateOverlay,
        float opacity,
        Rect backgroundRect)
    {
        var overlay = CreateImage(parent)
            .With(DefaultOverlayColor with { a = animateOverlay ? 0f : 0.8f })
            .With(backgroundRect)
            .WithRenderOrder(2);
        if (animateOverlay)
            overlay.With(components.ui.Fade.SingleIn(0.8f, opacity));
        overlay.Build();
    }

    public void CreateFullBackOverlay(DefaultEcs.Entity parent)
    {
        var overlay = CreateImage(parent)
            .With(DefaultOverlayColor with { a = 0f })
            .With(UI.LogicalScreen)
            .With(components.ui.UIOffset.ScreenUpperLeft)
            .WithRenderOrder(2)
            .With(components.ui.Fade.SingleIn(1.5f));
        overlay.Build();
    }

    public DefaultEcs.Entity CreateFullFlashFade(DefaultEcs.Entity parent, IColor color, components.ui.Fade flashFade) =>
        CreateImage(parent)
        .With(color)
        .With(components.ui.UIOffset.ScreenUpperLeft)
        .With(UI.LogicalScreen)
        .WithRenderOrder(100)
        .With(flashFade)
        .Build();

    public DefaultEcs.Entity CreateStdFlashFade(DefaultEcs.Entity parent) =>
        CreateFullFlashFade(parent, IColor.Black, new components.ui.Fade(
            From: 0f, To: 1f,
            StartDelay: 0f,
            InDuration: 0.3f,
            SustainDelay: 0.03f, // this ensures we have at least one frame of pure black to switch scenes/locations
            OutDuration: 0.3f));

    public DefaultEcs.Entity CreateCurrencyLabel(DefaultEcs.Entity parent, ItemRow currency, Inventory inventory) =>
        CreateLabel(parent)
            .With(new Vector2(-60, -170))
            .With(UIPreloadAsset.Fnt000)
            .WithText($"{GetDBText(UIDYouHave)} {{{3000 + currency.CardId.EntityId}}}x{inventory.CountCards(currency.CardId)}")
            .Build();

    public DefaultEcs.Entity CreateSingleDialogButton(DefaultEcs.Entity entity, UID textUID, components.ui.ElementId elementId, Rect bgRect, float buttonOffsetY = -50f)
    {
        var button = CreateButton(entity)
            .With(elementId)
            .With(new Vector2(bgRect.Center.X, bgRect.Max.Y + buttonOffsetY))
            .With(new components.ui.ButtonTiles(0, 1))
            .With(components.ui.FullAlignment.TopCenter)
            .With(UIPreloadAsset.Btn000)
            .WithLabel()
            .With(UIPreloadAsset.Fnt000)
            .WithText(textUID)
            .Build();

        // TODO: Set cursor position in dialog gambling
        return button;
    }

    public static string GetSpellPrices(SpellRow spellRow)
    {
        var sheet = spellRow.Type == 0 ? 5 : 4;
        return $"{{{sheet}{(int)spellRow.PriceA}}}{{{sheet}{(int)spellRow.PriceB}}}{{{sheet}{(int)spellRow.PriceC}}}";
    }

    public static string GetLightsIndicator(int value)
    {
        return string.Concat(Enumerable.Repeat("{1017}", value)) + string.Concat(Enumerable.Repeat("{1018}", 5 - value));
    }

    public string GetClassText(ZZClass zzClass) => zzClass switch
    {
        ZZClass.Nature => GetDBText(new UID(0x448DD8A1)), // Nature
        ZZClass.Air => GetDBText(new UID(0x30D5D8A1)), // Air
        ZZClass.Water => GetDBText(new UID(0xC15AD8A1)), // Water
        ZZClass.Light => GetDBText(new UID(0x6EE2D8A1)), // Light
        ZZClass.Energy => GetDBText(new UID(0x44AAD8A1)), // Energy
        ZZClass.Mental => GetDBText(new UID(0xEC31D8A1)), // Psi
        ZZClass.Stone => GetDBText(new UID(0xAD78D8A1)), // Stone
        ZZClass.Ice => GetDBText(new UID(0x6483DCA1)), // Ice
        ZZClass.Fire => GetDBText(new UID(0x8EC9DCA1)), // Fire
        ZZClass.Dark => GetDBText(new UID(0x8313DCA1)), // Dark
        ZZClass.Chaos => GetDBText(new UID(0xC659DCA1)), // Chaos
        ZZClass.Metal => GetDBText(new UID(0x3CE1DCA1)), // Metal
        ZZClass.Neutral => GetDBText(new UID(0x8B6BDCA1)), // Neutral
        _ => throw new ArgumentException($"Unknown spell class: {zzClass}")
    };
}
