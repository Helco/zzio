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

        public TileSheet(Image<Rgba32> image, bool isFont)
        {
            var tiles = new List<Rect>();
            var oneTexel = new Vector2(1f / image.Width, 1f / image.Height);
            var firstRow = image.GetPixelRowSpan(0);
            int tileStartX = 0;
            int tileEndXOffset = isFont ? -1 : 0;
            for (int tileEndX = 0; tileEndX < image.Width; tileEndX++)
            {
                if (firstRow[tileEndX] == new Rgba32(0, 0, 0, 255))
                    continue;

                // we might have to prevent color bleeding here...
                var min = new Vector2(tileStartX, 1f) * oneTexel;
                var max = new Vector2(tileEndX + tileEndXOffset, image.Height) * oneTexel;
                tiles.Add(Rect.FromMinMax(min, max));

                tileStartX = tileEndX + 1;
            }
            this.tiles = tiles.ToArray();
        }

        public IEnumerator<Rect> GetEnumerator() => ((IEnumerable<Rect>)tiles).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => tiles.GetEnumerator();
    }
}
