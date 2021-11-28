using System;
using System.Numerics;
using System.Collections.Generic;
using System.Collections;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace zzre.rendering
{
    public class TileSheet : IReadOnlyList<Rect>
    {
        private readonly Rect[] tiles;

        public Rect this[int index] => tiles[index];
        public int Count => tiles.Length;
        public Vector2 OneTexel { get; }
        public Vector2 TotalSize { get; }

        public TileSheet(Image<Rgba32> image, bool isFont)
        {
            var tiles = new List<Rect>();
            var height = image.Height - (isFont ? 0 : 1);
            TotalSize = new Vector2(image.Width, height);
            OneTexel = Vector2.One / TotalSize;
            var firstRow = image.GetPixelRowSpan(0);
            int tileStartX = 0;
            var tileEndXOffset = isFont ? -1f : 0f;
            var oneHalf = Vector2.One / 2f;
            for (int tileEndX = 0; tileEndX < image.Width; tileEndX++)
            {
                if (firstRow[tileEndX].R == 0 && firstRow[tileEndX].G == 0 && firstRow[tileEndX].B == 0 ||
                    tileEndX == tileStartX)
                    continue;

                var min = (new Vector2(tileStartX, isFont ? 0 : 1f) + oneHalf) * OneTexel;
                var max = (new Vector2(tileEndX + tileEndXOffset, height - 1) + oneHalf) * OneTexel;
                tiles.Add(Rect.FromMinMax(min, max));

                tileStartX = tileEndX + 1;
            }
            this.tiles = tiles.ToArray();
        }

        public Vector2 GetPixelSize(int tileId) => tiles[tileId].Size * TotalSize;

        public IEnumerator<Rect> GetEnumerator() => ((IEnumerable<Rect>)tiles).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => tiles.GetEnumerator();
    }
}
