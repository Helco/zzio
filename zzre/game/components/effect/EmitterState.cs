using System;
using System.Buffers;
using System.Numerics;

namespace zzre.game.components.effect;

// other name could be ParticleEmitterState but to prevent
// ParticleParticleEmitterState or ParticleEmitterParticleState we shorten.

public struct EmitterState(
    IMemoryOwner<EmitterState.Particle> particleMemoryOwner,
    int maxParticleCount)
{
    public float
        CurPhase1,
        CurPhase2,
        SpawnProgress;
    public int SpawnsLeft; // to be spawned this frame

    public readonly IMemoryOwner<Particle> ParticleMemoryOwner = particleMemoryOwner;
    public readonly Memory<Particle> Particles = particleMemoryOwner.Memory.Slice(0, maxParticleCount);

    public struct Particle
    {
        public float
            Life,
            MaxLife,
            Scale,
            ScaleMod;
        public Vector4 Color, ColorMod;
        public Vector3
            Pos,
            PrevPos,
            Velocity,
            Acceleration,
            Gravity,
            GravityMod; 
    }
}

public readonly struct ParticleEmitterState(
    IMemoryOwner<ParticleEmitterState.Particle> particleMemoryOwner,
    int maxParticleCount,
    Range vertexRange, Range indexRange)
{
    public struct Particle
    {
        public uint TileI;
        public float TileLife;
    }

    public readonly IMemoryOwner<Particle> ParticleMemoryOwner = particleMemoryOwner;
    public readonly Memory<Particle> Particles = particleMemoryOwner.Memory.Slice(0, maxParticleCount);
    public readonly Range VertexRange = vertexRange;
    public readonly Range IndexRange = indexRange;
}

public readonly struct ModelEmitterState(
    IMemoryOwner<ModelEmitterState.Particle> particleMemoryOwner,
    int maxParticleCount)
{
    public struct Particle
    {
        public Vector3 RotationAxis;
        public float
            Rotation,
            RotationSpeed;
    }

    public readonly IMemoryOwner<Particle> ParticleMemoryOwner = particleMemoryOwner;
    public readonly Memory<Particle> Particles = particleMemoryOwner.Memory.Slice(0, maxParticleCount);
}

public readonly struct SparkEmitterState(
    Range vertexRange, Range indexRange)
{
    public readonly Range VertexRange = vertexRange;
    public readonly Range IndexRange = indexRange;
}
