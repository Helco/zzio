using System;
using System.Numerics;
using DefaultEcs.Resource;
using zzio;
using zzre.rendering;

namespace zzre.game.systems.ui;

public class UIPreloader
{
    private const float ButtonTextSpacing = 10f;

    public static readonly FColor DefaultOverlayColor = new(0.029999999f, 0.050000001f, 0.029999999f, 0.8f);

    internal readonly DefaultEcs.World UIWorld;
    private readonly UI ui;
    private readonly zzio.db.MappedDB mappedDb;

    public readonly ManagedResource<resources.UITileSheetInfo, TileSheet>
        Btn000,
        Btn001,
        Btn002,
        Sld000,
        Tit000,
        Cur000,
        Dnd000,
        Wiz000,
        Itm000,
        Spl000,
        Lne000,
        Fnt000,
        Fnt001,
        Fnt002,
        Fnt003,
        Fnt004,
        Fsp000,
        Inf000,
        Log000,
        Cls000,
        Cls001,
        Map000;

    public UIPreloader(ITagContainer diContainer)
    {
        UIWorld = diContainer.GetTag<DefaultEcs.World>();
        ui = diContainer.GetTag<UI>();
        mappedDb = diContainer.GetTag<zzio.db.MappedDB>();

        Btn000 = Preload(out var tsBtn000, "btn000", isFont: false);
        Btn001 = Preload(out var tsBtn001, "btn001", isFont: false);
        Btn002 = Preload(out var tsBtn002, "btn002", isFont: false);
        Sld000 = Preload(out var tsSld000, "sld000", isFont: false);
        Tit000 = Preload(out var tsTit000, "tit000", isFont: false);
        Cur000 = Preload(out var tsCur000, "cur000", isFont: false);
        Dnd000 = Preload(out var tsDnd000, "dnd000", isFont: false);
        Wiz000 = Preload(out var tsWiz000, "wiz000", isFont: false);
        Itm000 = Preload(out var tsItm000, "itm000", isFont: false);
        Spl000 = Preload(out var tsSpl000, "spl000", isFont: false);
        Lne000 = Preload(out var tsLne000, "lne000", isFont: false);
        Fnt000 = Preload(out var tsFnt000, "fnt000", isFont: true, lineHeight: 14f, charSpacing: 1f);
        Fnt001 = Preload(out var tsFnt001, "fnt001", isFont: true, charSpacing: 1f);
        Fnt002 = Preload(out var tsFnt002, "fnt002", isFont: true, lineHeight: 17f, charSpacing: 1f, lineOffset: 2f);
        Fnt003 = Preload(out var tsFnt003, "fnt003", isFont: true, lineHeight: 14f, charSpacing: 1f);
        Fnt004 = Preload(out var tsFnt004, "fnt004", isFont: true, lineHeight: 14f, charSpacing: 1f);
        Fsp000 = Preload(out var tsFsp000, "fsp000", isFont: true);
        Inf000 = Preload(out var tsInf000, "inf000", isFont: false);
        Log000 = Preload(out var tsLog000, "log000", isFont: false);
        Cls000 = Preload(out var tsCls000, "cls000", isFont: false);
        Cls001 = Preload(out var tsCls001, "cls001", isFont: false);
        Map000 = Preload(out var tsMap000, "map000", isFont: false);

        tsFnt000.Alternatives.Add(tsFnt001);
        tsFnt000.Alternatives.Add(tsFnt002);
        tsFnt000.Alternatives.Add(tsFsp000);
        tsFnt000.Alternatives.Add(tsItm000);
        tsFnt000.Alternatives.Add(tsCls000);
        tsFnt000.Alternatives.Add(tsFnt003);
        tsFnt000.Alternatives.Add(tsLog000);
        tsFnt000.Alternatives.Add(tsCls001);
        tsFnt000.Alternatives.Add(tsFnt004);

        tsFnt001.Alternatives.Add(tsFnt002);
        tsFnt001.Alternatives.Add(tsInf000);
        tsFnt001.Alternatives.Add(tsFnt000);

        tsFnt002.Alternatives.Add(tsFnt001);
        tsFnt002.Alternatives.Add(tsInf000);
        tsFnt002.Alternatives.Add(tsFsp000);
        tsFnt002.Alternatives.Add(tsItm000);
        tsFnt002.Alternatives.Add(tsCls000);
        tsFnt002.Alternatives.Add(tsCls001);

        tsFnt003.Alternatives.Add(tsFnt001);
        tsFnt003.Alternatives.Add(tsFnt002);
        tsFnt003.Alternatives.Add(tsFsp000);
        tsFnt003.Alternatives.Add(tsItm000);
        tsFnt003.Alternatives.Add(tsFnt000);
        tsFnt003.Alternatives.Add(tsFnt000);
        tsFnt003.Alternatives.Add(tsLog000);
        tsFnt003.Alternatives.Add(tsCls001);
        tsFnt003.Alternatives.Add(tsFnt004);
    }

    private ManagedResource<resources.UITileSheetInfo, TileSheet> Preload(
        out TileSheet tileSheet,
        string name,
        bool isFont,
        float lineHeight = float.NaN,
        float charSpacing = float.NaN,
        float lineOffset = float.NaN)
    {
        var resource = ManagedResource<TileSheet>.Create(new resources.UITileSheetInfo(name, isFont));
        var entity = UIWorld.CreateEntity();
        entity.Set(resource);
        entity.Disable();
        tileSheet = entity.Get<TileSheet>();
        if (float.IsFinite(lineHeight))
            tileSheet.LineHeight = lineHeight;
        if (float.IsFinite(charSpacing))
            tileSheet.CharSpacing = charSpacing;
        if (float.IsFinite(lineOffset))
            tileSheet.LineOffset = lineOffset;
        return resource;
    }

    public ManagedResource<resources.UITileSheetInfo, TileSheet> GetTileSheetByCardType(CardType type) => type switch
    {
        CardType.Fairy => Wiz000,
        CardType.Item => Itm000,
        CardType.Spell => Spl000,
        _ => throw new NotSupportedException($"Unsupported card type {type}")
    };

    internal string GetDBText(zzio.UID textUID) => textUID.Module switch
    {
        (int)zzio.db.ModuleType.Text => mappedDb.GetText(textUID).Text,
        (int)zzio.db.ModuleType.Dialog => mappedDb.GetDialog(textUID).Text,
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
        out Rect backgroundRect)
    {
        var image = CreateImage(parent)
            .WithBitmap("std000")
            .With(components.ui.FullAlignment.Center)
            .WithRenderOrder(1)
            .Build();
        backgroundRect = image.Get<Rect>();

        var overlay = CreateImage(parent)
            .With(DefaultOverlayColor with { a = animateOverlay ? 0f : 0.8f })
            .With(backgroundRect)
            .WithRenderOrder(2);
        if (animateOverlay)
            overlay.With(new components.ui.Fade(0f, 0.8f, 1.5f));
        overlay.Build();
    }

    public DefaultEcs.Entity CreateFullFlashFade(DefaultEcs.Entity parent, IColor color, components.ui.FlashFade flashFade) =>
        CreateImage(parent)
        .With(color)
        .With(components.ui.UIOffset.ScreenUpperLeft)
        .With(ui.LogicalScreen)
        .WithRenderOrder(100)
        .With(flashFade)
        .Build();

    public DefaultEcs.Entity CreateStdFlashFade(DefaultEcs.Entity parent) =>
        CreateFullFlashFade(parent, IColor.Black, new components.ui.FlashFade(
            From: 0f, To: 1f,
            StartDelay: 0f,
            OutDuration: 0.3f,
            SustainDelay: 0f,
            InDuration: 0.3f));
}
