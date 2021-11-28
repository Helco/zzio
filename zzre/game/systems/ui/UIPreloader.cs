using System;
using DefaultEcs.Resource;
using zzre.rendering;

namespace zzre.game.systems.ui
{
    public class UIPreloader
    {
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
            var world = diContainer.GetTag<DefaultEcs.World>();

            Btn000 = Preload(world, out var tsBtn000, "btn000", isFont: false);
            Btn001 = Preload(world, out var tsBtn001, "btn001", isFont: false);
            Btn002 = Preload(world, out var tsBtn002, "btn002", isFont: false);
            Sld000 = Preload(world, out var tsSld000, "sld000", isFont: false);
            Tit000 = Preload(world, out var tsTit000, "tit000", isFont: false);
            Cur000 = Preload(world, out var tsCur000, "cur000", isFont: false);
            Dnd000 = Preload(world, out var tsDnd000, "dnd000", isFont: false);
            Wiz000 = Preload(world, out var tsWiz000, "wiz000", isFont: false);
            Itm000 = Preload(world, out var tsItm000, "itm000", isFont: false);
            Spl000 = Preload(world, out var tsSpl000, "spl000", isFont: false);
            Lne000 = Preload(world, out var tsLne000, "lne000", isFont: false);
            Fnt000 = Preload(world, out var tsFnt000, "fnt000", isFont: true, lineHeight: 14f, charSpacing: 1f);
            Fnt001 = Preload(world, out var tsFnt001, "fnt001", isFont: true, charSpacing: 1f);
            Fnt002 = Preload(world, out var tsFnt002, "fnt002", isFont: true, lineHeight: 17f, charSpacing: 1f, lineOffset: 2f);
            Fnt003 = Preload(world, out var tsFnt003, "fnt003", isFont: true, lineHeight: 14f, charSpacing: 1f);
            Fnt004 = Preload(world, out var tsFnt004, "fnt004", isFont: true, lineHeight: 14f, charSpacing: 1f);
            Fsp000 = Preload(world, out var tsFsp000, "fsp000", isFont: true);
            Inf000 = Preload(world, out var tsInf000, "inf000", isFont: false);
            Log000 = Preload(world, out var tsLog000, "log000", isFont: false);
            Cls000 = Preload(world, out var tsCls000, "cls000", isFont: false);
            Cls001 = Preload(world, out var tsCls001, "cls001", isFont: false);
            Map000 = Preload(world, out var tsMap000, "map000", isFont: false);

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
            DefaultEcs.World world,
            out TileSheet tileSheet,
            string name,
            bool isFont,
            float lineHeight = float.NaN,
            float charSpacing = float.NaN,
            float lineOffset = float.NaN)
        {
            var resource = ManagedResource<TileSheet>.Create(new resources.UITileSheetInfo(name, isFont));
            var entity = world.CreateEntity();
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
    }
}
