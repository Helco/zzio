using System;
using zzio;

namespace zzre.game.components.effect;

public struct BeamStarState(Range vertexRange, Range indexRange)
{
    public float
        CurPhase1,
        CurPhase2,
        CurScale,
        CurShrink,
        CurRotation,
        TexVRange,
        TexVStart;
    public FColor StartColor, EndColor;

    public readonly Range VertexRange = vertexRange;
    public readonly Range IndexRange = indexRange;
}
