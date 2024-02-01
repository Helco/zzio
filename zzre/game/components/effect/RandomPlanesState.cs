using System;
using System.Buffers;
using System.Numerics;

namespace zzre.game.components.effect;

public struct RandomPlanesState(
    IMemoryOwner<RandomPlanesState.RandomPlane> planeMemoryOwner,
    int maxPlaneCount,
    Range vertexRange, Range indexRange)
{
    public struct RandomPlane
    {
        public float
            Life,
            Rotation,
            RotationSpeed,
            Scale,
            ScaleSpeed;
        public Vector3 Pos;
        public Vector4 StartColor, CurColor;
        public uint TileI;
        public float TileProgress;
    }

    public float
        CurPhase1,
        CurPhase2,
        CurTexShift,
        SpawnProgress;

    public readonly IMemoryOwner<RandomPlane> PlaneMemoryOwner = planeMemoryOwner;
    public readonly Memory<RandomPlane> Planes = planeMemoryOwner.Memory.Slice(0, maxPlaneCount);
    public readonly Range
        VertexRange = vertexRange,
        IndexRange = indexRange;
}

