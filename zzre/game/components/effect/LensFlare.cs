using System;

namespace zzre.game.components.effect;

public struct LensFlare(Range vertexRange, Range indexRange, int type)
{
    public readonly Range VertexRange = vertexRange, IndexRange = indexRange;
    public readonly int Type = type;

    public float CurAlpha;
}
