using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using DefaultEcs.Resource;
using DefaultEcs.System;
using zzre.rendering;

namespace zzre.game.systems.ui
{
    [With(typeof(components.ui.LabelNeedsTiling))]
    public partial class Label : AEntitySetSystem<float>
    {
        // at least a small regex for the special group, the code to do this manually really isn't nice
        private static readonly Regex GroupRegex = new Regex(
            @"\G{(?:" +
            @"(\d\*)|" + // change font
            @"(t\d{1,3})|" + // set cursor position
            @"(\d{2,4})" + // add single icon from another font
            ")",
            RegexOptions.Compiled);

        private readonly IDisposable addedSubscription;
        private readonly IDisposable changedSubscription;

        public Label(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
        {
            addedSubscription = World.SubscribeComponentAdded<components.ui.Label>(SetLabelNeedsTiling);
            changedSubscription = World.SubscribeComponentChanged<components.ui.Label>(SetLabelNeedsTiling);
        }

        public override void Dispose()
        {
            base.Dispose();
            addedSubscription.Dispose();
            changedSubscription.Dispose();
        }

        [WithPredicate]
        private bool IsVisible(in components.Visibility visibility) => visibility == components.Visibility.Visible;

        private void SetLabelNeedsTiling(in DefaultEcs.Entity entity, in components.ui.Label _)
        {
            entity.Set<components.ui.LabelNeedsTiling>();
            if (!entity.Has<components.ui.Tile[]>())
                entity.Set(Array.Empty<components.ui.Tile>());
        }

        private void SetLabelNeedsTiling(in DefaultEcs.Entity entity, in components.ui.Label oldValue, in components.ui.Label newValue)
        {
            if (oldValue != newValue)
                entity.Set<components.ui.LabelNeedsTiling>();
        }

        [Update]
        private void Update(
            DefaultEcs.Entity entity,
            in Rect rect,
            TileSheet rootTileSheet,
            in components.ui.Label label,
            ref components.ui.Tile[] tiles)
        {
            entity.Remove<components.ui.LabelNeedsTiling>();

            var (text, doFormat) = label;
            if (!doFormat)
            {
                tiles = TileWithoutFormatting(rect, rootTileSheet, text);
                return;
            }

            var newTiles = FormatToTiles(rect, rootTileSheet, text);
            tiles = newTiles
                .Where(t => t.tileSheet == rootTileSheet)
                .Select(t => t.tile)
                .ToArray();

            var oldSubLabels = World
                .GetEntities()
                .With<components.ui.SubLabel>()
                .With((in components.Parent p) => p.Entity == entity)
                .AsEnumerable();
            foreach (var oldSubLabel in oldSubLabels)
                oldSubLabel.Dispose();

            var newSubLabels = newTiles
                .GroupBy(t => t.tileSheet)
                .Where(g => g.Key != rootTileSheet);
            foreach (var group in newSubLabels)
                CreateSubLabel(entity, group);
        }

        private void CreateSubLabel(DefaultEcs.Entity parent, IGrouping<TileSheet, (TileSheet, components.ui.Tile tile)> tiles)
        {
            var entity = World.CreateEntity();
            entity.SetSameAs<Rect>(parent);
            entity.SetSameAs<zzio.IColor>(parent);
            entity.SetSameAs<components.ui.RenderOrder>(parent);
            entity.SetSameAs<components.Visibility>(parent);
            entity.SetSameAs<components.ui.UIOffset>(parent);
            entity.Set<components.ui.SubLabel>();
            entity.Set(tiles.Select(t => t.tile).ToArray());
            entity.Set(new components.Parent(parent));
            entity.Set(ManagedResource<TileSheet>.Create(new resources.UITileSheetInfo(tiles.Key.Name, tiles.Key.IsFont)));
        }

        private static IReadOnlyList<(TileSheet tileSheet, components.ui.Tile tile)> FormatToTiles(in Rect rect, TileSheet rootTileSheet, string text)
        {
            var curTileSheet = rootTileSheet;
            var lineOffset = 0f;
            var cursor = rect.Min;
            var newTiles = new List<(TileSheet, components.ui.Tile)>(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                switch (text[i])
                {
                    case '\r': break;

                    case '\n':
                    case '~':
                        cursor.X = rect.Min.X;
                        cursor.Y += rootTileSheet.LineHeight;
                        break;

                    case '}':
                        curTileSheet = rootTileSheet;
                        lineOffset = 0f;
                        break;

                    case '{':
                        var match = GroupRegex.Match(text, i);
                        if (!match.Success)
                            throw new ArgumentException("Invalid label format");
                        if (match.Groups[1].Success)
                            ChangeTileSheet(match.Groups[1].Value[0]);
                        if (match.Groups[2].Success)
                            cursor.X = rect.Min.X + int.Parse(match.Groups[2].Value[1..]);
                        if (match.Groups[3].Success)
                            AddSpecialTile(match.Groups[3].Value);
                        i += match.Length - 1;
                        break;

                    case ' ':
                        var pixelSize = curTileSheet.GetPixelSize(0);
                        cursor.X += pixelSize.X + curTileSheet.CharSpacing;
                        break;

                    default:
                        CreateTile(text[i] - ' ', curTileSheet);
                        break;
                }
            }
            return newTiles;

            void ChangeTileSheet(char tileSheetIdentifier)
            {
                int alternativeI = tileSheetIdentifier - '0';
                if (alternativeI < 0 || alternativeI >= rootTileSheet.Alternatives.Count)
                    return;
                curTileSheet = rootTileSheet.Alternatives[alternativeI];
                lineOffset = (rootTileSheet.TotalSize.Y - curTileSheet.TotalSize.Y) / 2f;
            }

            void AddSpecialTile(string identifier)
            {
                int alternativeI = identifier[0] - '0';
                int tileI = int.Parse(identifier[1..]);
                if (alternativeI < 0 || alternativeI >= rootTileSheet.Alternatives.Count)
                    return;
                CreateTile(tileI, rootTileSheet.Alternatives[alternativeI]);
            }

            void CreateTile(int tileI, TileSheet curTileSheet)
            {
                if (tileI < 0 || tileI >= curTileSheet.Count)
                    return;
                var pixelSize = curTileSheet.GetPixelSize(tileI);
                var lineOffset = rootTileSheet.LineOffset +
                    (rootTileSheet.TotalSize.Y - curTileSheet.TotalSize.Y) / 2;
                var tile = new components.ui.Tile(
                    tileI,
                    TileRect(cursor, pixelSize, lineOffset));
                cursor.X += pixelSize.X + curTileSheet.CharSpacing;
                newTiles.Add((curTileSheet, tile));
            }
        }

        private components.ui.Tile[] TileWithoutFormatting(in Rect rect, TileSheet tileSheet, string text)
        {
            var tiles = new List<components.ui.Tile>(text.Length);
            var cursor = rect.Min;
            foreach (var ch in text)
            {
                int tileI = ch - ' ';
                if (tileI < 0 || tileI >= tileSheet.Count)
                    continue;
                var pixelSize = tileSheet.GetPixelSize(tileI);
                if (ch != ' ')
                    tiles.Add(new(tileI, TileRect(cursor, pixelSize)));
                cursor.X += pixelSize.X;
            }
            return tiles.ToArray();
        }

        private static Rect TileRect(Vector2 cursor, Vector2 pixelSize, float lineOffset = 0f)
        {
            cursor.Y -= lineOffset;
            return Rect.FromMinMax(cursor, cursor + pixelSize);
        }
    }
}
