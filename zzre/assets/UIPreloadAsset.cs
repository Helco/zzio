using System;
using System.Linq;
using System.Threading;
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

public sealed class UIPreloadAsset(IAssetRegistry registry) : IAsset<UIPreloadAsset.Info>
{
    public static readonly UITileSheetAsset.Info
        Btn000 = new("btn000"),
        Btn001 = new("btn001"),
        Btn002 = new("btn002"),
        Sld000 = new("sld000"),
        Tit000 = new("tit000"),
        Cur000 = new("cur000"),
        Dnd000 = new("dnd000"),
        Wiz000 = new("wiz000"),
        Itm000 = new("itm000"),
        Spl000 = new("spl000"),
        Lne000 = new("lne000"),
        Fnt000 = new("fnt000", LineHeight: 14f, CharSpacing: 1f),
        Fnt001 = new("fnt001", CharSpacing: 1f),
        Fnt002 = new("fnt002", LineHeight: 17f, CharSpacing: 1f, LineOffset: 2f),
        Fnt003 = new("fnt003", LineHeight: 14f, CharSpacing: 1f),
        Fnt004 = new("fnt004", LineHeight: 14f, CharSpacing: 1f),
        Fsp000 = new("fsp000", CharSpacing: 0f),
        Inf000 = new("inf000"),
        Log000 = new("log000"),
        Cls000 = new("cls000"),
        Cls001 = new("cls001"),
        Map000 = new("map000"),
        Swt000 = new("swt000");

    public readonly record struct Info;

    private AssetHandle<UITileSheetAsset>[] handles = [];

    static AssetLocality IAsset.Locality => AssetLocality.Local; // the bitmaps are local
    public IAssetRegistry Registry { get; } = registry;

    static async Task<AssetLoadResult<Info>> IAsset<Info>.LoadAsync(IAssetRegistry registry, Guid _, Info info, CancellationToken ct)
    {
        AssetHandle<UITileSheetAsset> Preload(out AssetHandle<UITileSheetAsset> handle, UITileSheetAsset.Info info) =>
            handle = registry.LoadUITileSheet(info, AssetPriority.High);

        AssetHandle<UITileSheetAsset>[] allAssets =
        [
            Preload(out var btn000, Btn000),
            Preload(out var btn001, Btn001),
            Preload(out var btn002, Btn002),
            Preload(out var sld000, Sld000),
            Preload(out var tit000, Tit000),
            Preload(out var cur000, Cur000),
            Preload(out var dnd000, Dnd000),
            Preload(out var wiz000, Wiz000),
            Preload(out var itm000, Itm000),
            Preload(out var spl000, Spl000),
            Preload(out var lne000, Lne000),
            Preload(out var fnt000, Fnt000),
            Preload(out var fnt001, Fnt001),
            Preload(out var fnt002, Fnt002),
            Preload(out var fnt003, Fnt003),
            Preload(out var fnt004, Fnt004),
            Preload(out var fsp000, Fsp000),
            Preload(out var inf000, Inf000),
            Preload(out var log000, Log000),
            Preload(out var cls000, Cls000),
            Preload(out var cls001, Cls001),
            Preload(out var map000, Map000),
            Preload(out var swt000, Swt000)
        ];

        await Task.WhenAll(allAssets.Select(h => h.GetAsync(ct).AsTask())).WaitAsync(ct);

        SetAlternatives(target: fnt000,
            fnt001,
            fnt002,
            fsp000,
            itm000,
            cls000,
            fnt003,
            log000,
            cls001,
            fnt004);

        SetAlternatives(target: fnt001,
            fnt002,
            inf000,
            fnt000);

        SetAlternatives(target: fnt002,
            fnt001,
            inf000,
            fsp000,
            itm000,
            cls000,
            cls001);

        SetAlternatives(target: fnt003,
            fnt001,
            fnt002,
            fsp000,
            itm000,
            fnt000,
            fnt000, // yes fnt000 is twice in the alternatives
            log000,
            cls001,
            fnt004);

        return new(new UIPreloadAsset(registry)
        {
            handles = allAssets
        });
    }

    private static void SetAlternatives(AssetHandle<UITileSheetAsset> target, params AssetHandle<UITileSheetAsset>[] alternatives)
    {
        var targetTileSheet = target.Asset?.TileSheet
            ?? throw new InvalidOperationException("Secondary asset was not loaded");
        foreach (var alternative in alternatives)
            targetTileSheet.Alternatives.Add(alternative.Asset?.TileSheet
                ?? throw new InvalidOperationException("Secondary asset was not loaded"));
    }
    
    public void Dispose()
    {
        foreach (var handle in handles)
            handle.Dispose();
        handles = [];
    }
}
