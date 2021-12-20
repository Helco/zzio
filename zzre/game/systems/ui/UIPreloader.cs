﻿using System;
using System.Numerics;
using DefaultEcs.Resource;
using zzre.rendering;

namespace zzre.game.systems.ui
{
    public class UIPreloader
    {
        private const float ButtonTextSpacing = 10f;

        private readonly DefaultEcs.World world;
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
            world = diContainer.GetTag<DefaultEcs.World>();
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

        private DefaultEcs.Entity CreateBase(
            DefaultEcs.Entity parent,
            Rect rect,
            int renderOrder,
            components.ui.UIOffset? offset)
        {
            var entity = world.CreateEntity();
            entity.Set(new components.Parent(parent));
            entity.Set(new components.ui.RenderOrder(renderOrder));
            entity.Set(components.Visibility.Visible);
            entity.Set(zzio.IColor.White);
            entity.Set(rect);
            entity.Set(offset ?? components.ui.UIOffset.Center);
            return entity;
        }

        public DefaultEcs.Entity CreateImageButton(
            DefaultEcs.Entity parent,
            components.ui.ElementId elementId,
            Vector2 pos,
            components.ui.ButtonTiles buttonTiles,
            ManagedResource<resources.UITileSheetInfo, TileSheet> tileSheet,
            int renderOrder = 0,
            components.ui.UIOffset? offset = null) =>
            CreateImageButton(parent, elementId, Rect.FromMinMax(pos, pos), buttonTiles, tileSheet, renderOrder, offset);

        public DefaultEcs.Entity CreateImageButton(
            DefaultEcs.Entity parent,
            components.ui.ElementId elementId,
            Rect rect,
            components.ui.ButtonTiles buttonTiles,
            ManagedResource<resources.UITileSheetInfo, TileSheet> tileSheet,
            int renderOrder = 0,
            components.ui.UIOffset? offset = null)
        {
            var entity = CreateBase(parent, rect, renderOrder, offset);
            entity.Set(elementId);
            entity.Set(tileSheet);
            entity.Set(buttonTiles);
            return entity;
        }

        public DefaultEcs.Entity CreateLabel(
            DefaultEcs.Entity parent,
            Vector2 pos,
            zzio.UID textUID,
            ManagedResource<resources.UITileSheetInfo, TileSheet> font,
            int renderOrder = 0,
            bool doFormat = true,
            components.ui.UIOffset? offset = null) =>
            CreateLabel(parent, pos, GetDBText(textUID), font, renderOrder, doFormat, offset);

        private string GetDBText(zzio.UID textUID) => textUID.Module switch
        {
            (int)zzio.db.ModuleType.Text => mappedDb.GetText(textUID).Text,
            (int)zzio.db.ModuleType.Dialog => mappedDb.GetDialog(textUID).Text,
            _ => throw new ArgumentException($"Invalid UID for UI")
        };

        public DefaultEcs.Entity CreateLabel(
            DefaultEcs.Entity parent,
            Vector2 pos,
            string text,
            ManagedResource<resources.UITileSheetInfo, TileSheet> font,
            int renderOrder = 0,
            bool doFormat = true,
            components.ui.UIOffset? offset = null)
        {
            var entity = CreateBase(parent, Rect.FromMinMax(pos, pos), renderOrder, offset);
            entity.Set(font);
            entity.Set(new components.ui.Label(text, doFormat));
            return entity;
        }

        public DefaultEcs.Entity CreateButton(
            DefaultEcs.Entity parent,
            components.ui.ElementId elementId,
            Vector2 pos,
            zzio.UID textUID,
            components.ui.ButtonTiles buttonTiles,
            ManagedResource<resources.UITileSheetInfo, TileSheet> border,
            ManagedResource<resources.UITileSheetInfo, TileSheet> font,
            out DefaultEcs.Entity label,
            int renderOrder = 0,
            bool doFormat = true,
            components.ui.TextAlignment align = components.ui.TextAlignment.Center,
            components.ui.UIOffset? offset = null) =>
            CreateButton(parent, elementId, pos, GetDBText(textUID), buttonTiles, border, font, out label, renderOrder, doFormat, align, offset);

        public DefaultEcs.Entity CreateButton(
            DefaultEcs.Entity parent,
            components.ui.ElementId elementId,
            Vector2 pos,
            string text,
            components.ui.ButtonTiles buttonTiles,
            ManagedResource<resources.UITileSheetInfo, TileSheet> border,
            ManagedResource<resources.UITileSheetInfo, TileSheet> font,
            out DefaultEcs.Entity label,
            int renderOrder = 0,
            bool doFormat = true,
            components.ui.TextAlignment align = components.ui.TextAlignment.Center,
            components.ui.UIOffset? offset = null)
        {
            var button = CreateImageButton(parent, elementId, pos, buttonTiles, border, renderOrder, offset);

            label = CreateLabel(button, pos, text, font, renderOrder - 1, doFormat, offset);
            var fontTileSheet = label.Get<TileSheet>();
            var buttonSize = button.Get<Rect>().Size;
            var textWidth = fontTileSheet.GetUnformattedWidth(text);
            var labelPosX = align switch
            {
                components.ui.TextAlignment.Left => ButtonTextSpacing,
                components.ui.TextAlignment.Right => buttonSize.X - textWidth - ButtonTextSpacing,
                components.ui.TextAlignment.Center => buttonSize.X / 2 - textWidth / 2,
                _ => throw new NotSupportedException($"Unsupported text alignment {align}")
            };
            var labelPos = pos + new Vector2(labelPosX, buttonSize.Y / 2 - fontTileSheet.TotalSize.Y / 2);
            label.Set(Rect.FromMinMax(labelPos, labelPos));
            label.Set<components.ui.LabelNeedsTiling>(); // we changed the position

            return button;
        }

        public DefaultEcs.Entity CreateImage(
            DefaultEcs.Entity parent,
            Vector2 pos,
            string bitmap,
            int renderOrder,
            components.ui.UIOffset? offset = null)
        {
            var entity = CreateBase(parent, Rect.FromMinMax(pos, pos), renderOrder, offset);
            entity.Set(ManagedResource<materials.UIMaterial>.Create(bitmap));
            var texture = entity.Get<materials.UIMaterial>().Texture.Texture!;
            var rect = Rect.FromMinMax(pos, pos + new Vector2(texture.Width, texture.Height));
            entity.Set(rect);
            entity.Set(new components.ui.Tile[] { new(-1, rect) });
            return entity;
        }
    }
}
