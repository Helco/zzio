using System;

namespace zzre.game.components.effect;

public struct MovingPlanesState(Range vertexRange, Range indexRange, Rect texCoords)
{
    public float
        CurRotation,
        CurTexShift,
        CurScale,
        CurPhase1,
        CurPhase2,
        PrevProgress;

    public readonly Range VertexRange = vertexRange;
    public readonly Range IndexRange = indexRange;
    public readonly Rect TexCoords = texCoords;
}
