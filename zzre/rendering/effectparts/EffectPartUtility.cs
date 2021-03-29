using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using zzre.materials;

namespace zzre.rendering.effectparts
{
    internal static class EffectPartUtility
    {
        private const int OriginalTexSize = 256;

        public static Rect GetTileUV(uint tileW, uint tileH, uint tileId)
        {
            float texTileW = tileW / (float)OriginalTexSize;
            float texTileH = tileH / (float)OriginalTexSize;
            uint tilesInX = OriginalTexSize / tileW;
            return new Rect(new Vector2(
                ((tileId % tilesInX) + 0.5f) * texTileW,
                ((tileId / tilesInX) + 0.5f) * texTileH),
                new Vector2(texTileW, texTileH));
        }

        public static Rect[] GetTileUV(uint tileW, uint tileH, uint tileId, uint tileCount) => Enumerable
            .Range(0, (int)tileCount)
            .Select(i => GetTileUV(tileW, tileH, tileId + (uint)i))
            .ToArray();

        public static void UpdateQuad(this Span<EffectVertex> vertices, Vector3 center, Vector3 right, Vector3 up, Vector4 color, Rect texCoords)
        {
            vertices[0].pos = -right + -up;
            vertices[1].pos = -right + up;
            vertices[2].pos = right + up;
            vertices[3].pos = right + -up;
            vertices[0].tex = new Vector2(texCoords.Min.X, texCoords.Min.Y);
            vertices[1].tex = new Vector2(texCoords.Min.X, texCoords.Max.Y);
            vertices[2].tex = new Vector2(texCoords.Max.X, texCoords.Max.Y);
            vertices[3].tex = new Vector2(texCoords.Max.X, texCoords.Min.Y); ;

            for (int i = 0; i < 4; i++)
            {
                vertices[i].center = center;
                vertices[i].color = color;
            }
        }
    }
}
