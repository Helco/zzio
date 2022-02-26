using System;
using System.Numerics;
using System.Collections.Generic;
using System.Collections;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Linq;

namespace zzre.rendering
{
    public class TileSheet : IReadOnlyList<Rect>
    {
        private readonly Rect[] tiles;
        private readonly Vector2[] pixelSizes;

        public string Name { get; }
        public bool IsFont { get; }
        public Rect this[int index] => tiles[index];
        public int Count => tiles.Length;
        public Vector2 OneTexel { get; }
        public Vector2 TotalSize { get; }
        public float LineHeight { get; set; }
        public float LineOffset { get; set; }
        public float CharSpacing { get; set; }
        public IList<TileSheet> Alternatives { get; } = new List<TileSheet>();

        public TileSheet(string name, Image<Rgba32> image, bool isFont)
        {
            Name = name;
            IsFont = isFont;
            var height = image.Height - 1;
            TotalSize = new Vector2(image.Width, height);
            OneTexel = Vector2.One / TotalSize;
            LineHeight = TotalSize.Y;

            var firstRow = image.GetPixelRowSpan(0);
            int tileStartX = 0;
            var tileEndXOffset = isFont ? 0 : 1;
            var tiles = new List<Rect>();
            var pixelSizes = new List<Vector2>();
            for (int tileEndX = 0; tileEndX < image.Width; tileEndX++)
            {
                if (firstRow[tileEndX].R == 0 && firstRow[tileEndX].G == 0 && firstRow[tileEndX].B == 0 ||
                    tileEndX == tileStartX)
                    continue;

                var pixelSize = new Vector2(tileEndX - tileStartX + tileEndXOffset, height);
                var min = new Vector2(tileStartX, 0f) * OneTexel;
                var max = min + pixelSize * OneTexel;
                tiles.Add(Rect.FromMinMax(min, max));
                pixelSizes.Add(pixelSize);

                tileStartX = tileEndX + 1;
            }
            this.tiles = tiles.ToArray();
            this.pixelSizes = pixelSizes.ToArray();
        }

        public Vector2 GetPixelSize(int tileId) => pixelSizes[tileId];

        public float GetUnformattedWidth(string text) => text
            .Select(ch => ch - ' ')
            .Where(tileI => tileI >= 0 && tileI < pixelSizes.Length)
            .Sum(tileI => pixelSizes[tileI].X + CharSpacing);

        public float GetTextHeight(string text, float? overrideLineHeight = null) =>
            text.Count(ch => ch == '\n') * (overrideLineHeight ?? LineHeight);

        private static readonly char[] SpaceChars = new[] { ' ', '\n' };
        public string WrapLines(string text, float maxWidth)
        {
            var newText = text.ToCharArray();
            float spaceWidth = pixelSizes.First().X;
            float curLineWidth = 0;
            int nonSpaceI = text.IndexOfAnyNot(SpaceChars);
            int lastSpaceI = 0;
            while(nonSpaceI >= 0)
            {
                int spaceI = text.IndexOfAny(SpaceChars, nonSpaceI);
                float wordWidth = GetUnformattedWidth(spaceI < 0
                    ? text[nonSpaceI..]
                    : text[nonSpaceI..spaceI]);
                wordWidth += spaceWidth * (nonSpaceI - lastSpaceI);

                if (spaceI >= 0 && text[spaceI] == '\n')
                    curLineWidth = 0f;
                else
                {
                    if (curLineWidth + wordWidth <= maxWidth)
                        curLineWidth += wordWidth;
                    else
                    {
                        if (wordWidth > maxWidth && spaceI >= 0)
                            newText[spaceI] = '\n';

                        if (lastSpaceI > 0)
                            newText[lastSpaceI] = '\n';

                        curLineWidth = wordWidth;
                    }
                }

                lastSpaceI = spaceI;
                nonSpaceI = text.IndexOfAnyNot(SpaceChars, spaceI);
            }
            return new string(newText);
        }

        public IEnumerator<Rect> GetEnumerator() => ((IEnumerable<Rect>)tiles).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => tiles.GetEnumerator();
    }
}
