using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace zzre;

// the UI tile sheets have very unfortunate inter-dependencies
// In order to load the asynchronously we also have to wait for all
// of them load, then apply the TileSheet alternatives
//
// in case you wonder: No we cannot put the dependent TileSheets 
// into the secondary assets of the parent TileSheets as they
// form circular dependencies (fnt000 -> fnt001 -> fnt000) 
// which cannot be loaded by the AssetRegistry.

public sealed class UIPreloadAsset : Asset
{
    public static readonly UITileSheetAsset.Info
        Btn000 = new("btn000", IsFont: false),
        Btn001 = new("btn001", IsFont: false),
        Btn002 = new("btn002", IsFont: false),
        Sld000 = new("sld000", IsFont: false),
        Tit000 = new("tit000", IsFont: false),
        Cur000 = new("cur000", IsFont: false),
        Dnd000 = new("dnd000", IsFont: false),
        Wiz000 = new("wiz000", IsFont: false),
        Itm000 = new("itm000", IsFont: false),
        Spl000 = new("spl000", IsFont: false),
        Lne000 = new("lne000", IsFont: false),
        Fnt000 = new("fnt000", IsFont: true),
        Fnt001 = new("fnt001", IsFont: true),
        Fnt002 = new("fnt002", IsFont: true),
        Fnt003 = new("fnt003", IsFont: true),
        Fnt004 = new("fnt004", IsFont: true),
        Fsp000 = new("fsp000", IsFont: true),
        Inf000 = new("inf000", IsFont: false),
        Log000 = new("log000", IsFont: false),
        Cls000 = new("cls000", IsFont: false),
        Cls001 = new("cls001", IsFont: false),
        Map000 = new("map000", IsFont: false);

    public readonly record struct Info;

    public static void Register() =>
        AssetInfoRegistry<Info>.Register<UIPreloadAsset>(AssetLocality.Context);

    public UIPreloadAsset(IAssetRegistry registry, Guid assetId, Info _) : base(registry, assetId)
    { }

    protected override async ValueTask<IEnumerable<AssetHandle>> Load()
    {
        AssetHandle[] allAssets =
        [
            Preload(out var btn000, "btn000", isFont: false),
            Preload(out var btn001, "btn001", isFont: false),
            Preload(out var btn002, "btn002", isFont: false),
            Preload(out var sld000, "sld000", isFont: false),
            Preload(out var tit000, "tit000", isFont: false),
            Preload(out var cur000, "cur000", isFont: false),
            Preload(out var dnd000, "dnd000", isFont: false),
            Preload(out var wiz000, "wiz000", isFont: false),
            Preload(out var itm000, "itm000", isFont: false),
            Preload(out var spl000, "spl000", isFont: false),
            Preload(out var lne000, "lne000", isFont: false),
            Preload(out var fnt000, "fnt000", isFont: true, lineHeight: 14f, charSpacing: 1f),
            Preload(out var fnt001, "fnt001", isFont: true, charSpacing: 1f),
            Preload(out var fnt002, "fnt002", isFont: true, lineHeight: 17f, charSpacing: 1f, lineOffset: 2f),
            Preload(out var fnt003, "fnt003", isFont: true, lineHeight: 14f, charSpacing: 1f),
            Preload(out var fnt004, "fnt004", isFont: true, lineHeight: 14f, charSpacing: 1f),
            Preload(out var fsp000, "fsp000", isFont: true),
            Preload(out var inf000, "inf000", isFont: false),
            Preload(out var log000, "log000", isFont: false),
            Preload(out var cls000, "cls000", isFont: false),
            Preload(out var cls001, "cls001", isFont: false),
            Preload(out var map000, "map000", isFont: false)
        ];

        await Registry.WaitAsyncAll([fnt000, fnt001, fnt002, fnt003]);

        await SetAlternatives(target: fnt000,
            fnt001,
            fnt002,
            fsp000,
            itm000,
            cls000,
            fnt003,
            log000,
            cls001,
            fnt004);

        await SetAlternatives(target: fnt001,
            fnt002,
            inf000,
            fnt000);

        await SetAlternatives(target: fnt002,
            fnt001,
            inf000,
            fsp000,
            itm000,
            cls000,
            cls001);

        await SetAlternatives(target: fnt003,
            fnt001,
            fnt002,
            fsp000,
            itm000,
            fnt000,
            fnt000, // yes fnt000 is twice in the alternatives
            log000,
            cls001,
            fnt004);

        return allAssets;
    }

    protected override void Unload()
    { } // we only have secondary assets

    private unsafe AssetHandle Preload(out AssetHandle<UITileSheetAsset> handle, string name, bool isFont,
        float? lineHeight = null,
        float? lineOffset = null,
        float? charSpacing = null)
    {
        var applyConfig = (lineHeight ?? lineOffset ?? charSpacing) is not null;
        return handle = (applyConfig
            ? Registry.Load(new UITileSheetAsset.Info(name, isFont), AssetLoadPriority.High, &ApplyFontConfig, (lineHeight, lineOffset, charSpacing))
            : Registry.Load(new UITileSheetAsset.Info(name, isFont), AssetLoadPriority.High))
            .As<UITileSheetAsset>();
    }

    private static void ApplyFontConfig(AssetHandle handle, ref readonly (float?, float?, float?) config)
    {
        var tileSheet = handle.Get<UITileSheetAsset>().TileSheet;
        var (lineHeight, lineOffset, charSpacing) = config;
        if (lineHeight is not null)
            tileSheet.LineHeight = lineHeight.Value;
        if (lineOffset is not null)
            tileSheet.LineOffset = lineOffset.Value;
        if (charSpacing is not null)
            tileSheet.CharSpacing = charSpacing.Value;
    }

    private async Task SetAlternatives(AssetHandle target, params AssetHandle[] alternatives)
    {
        await Registry.WaitAsyncAll(alternatives);
        var targetTileSheet = target.Get<UITileSheetAsset>().TileSheet;
        foreach (var alternative in alternatives)
            targetTileSheet.Alternatives.Add(alternative.Get<UITileSheetAsset>().TileSheet);
    }
}
