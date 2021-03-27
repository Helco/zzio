using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

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
    }
}
