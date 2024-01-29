using System;
using System.Numerics;

namespace zzre.game.components.effect;

public struct CombinerPlayback(float duration, float curProgress = 100f, float length = 1f)
{
    public float
        CurTime = 0f,
        CurProgress = curProgress,
        Length = length,
        Duration = duration; // set to infinite to loop
    public bool IsFinished => CurTime >= Duration;
    public bool IsRunning => !IsFinished && !MathEx.CmpZero(CurProgress);
    public bool IsLooping => Duration == float.PositiveInfinity;
}

// TODO: Move into own files
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
