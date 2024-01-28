using System;
using System.Numerics;

namespace zzre.game.components.effect;

public struct CombinerPlayback
{
    public float
        CurTime,
        CurProgress,
        Length;
    public bool IsLooping;
}

// TODO: Move into own files
public struct MovingPlanesState
{
    public float
        CurRotation,
        CurTexShift,
        CurScale,
        CurPhase1,
        CurPhase2,
        PrevProgress;
    public Vector4 CurColor;

    public readonly Range VertexRange;
    public readonly Rect TexCoords;
}
