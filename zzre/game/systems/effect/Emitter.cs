using System;
using System.Buffers;
using System.Numerics;
using zzio;

namespace zzre.game.systems.effect;

public sealed class Emitter : BaseCombinerPart<
    zzio.effect.parts.ParticleEmitter,
    components.effect.EmitterState>
{
    private const float GravityFactor = 9.8f;

    private readonly MemoryPool<components.effect.EmitterState.Particle> particleMemoryPool;

    public Emitter(ITagContainer diContainer) : base(diContainer)
    {
        particleMemoryPool = MemoryPool<components.effect.EmitterState.Particle>.Shared;
    }

    public override void Dispose()
    {
        base.Dispose();
        particleMemoryPool.Dispose();
    }

    protected override void HandleRemovedComponent(in DefaultEcs.Entity entity, in components.effect.EmitterState state)
    {
        state.ParticleMemoryOwner.Dispose();
    }

    protected override void HandleAddedComponent(in DefaultEcs.Entity entity, in zzio.effect.parts.ParticleEmitter data)
    {
        int maxParticleCount = (int)(data.spawnRate * data.life.value );
        var particleMemoryOwner = particleMemoryPool.Rent(maxParticleCount);
        entity.Set(new components.effect.EmitterState(
            particleMemoryOwner,
            maxParticleCount));
        Reset(ref entity.Get<components.effect.EmitterState>(), data);
    }

    private void Reset(ref components.effect.EmitterState state, zzio.effect.parts.ParticleEmitter data)
    {
        state.CurPhase1 = data.phase1 / 1000f;
        state.CurPhase2 = data.phase2 / 1000f;
        state.SpawnProgress = 0f;
        state.SpawnsLeft = 0;
        foreach (ref var particle in state.Particles.Span)
            particle.Life = -1f;
    }

    protected override void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        in components.Parent parent,
        ref components.effect.EmitterState state,
        in zzio.effect.parts.ParticleEmitter data,
        ref components.effect.RenderIndices indices)
    {
        UpdateSpawning(elapsedTime, parent, ref state, data);
        foreach (ref var particle in state.Particles.Span)
            UpdateParticle(elapsedTime, ref particle);
    }

    private void UpdateSpawning(
        float elapsedTime,
        in components.Parent parent,
        ref components.effect.EmitterState state,
        in zzio.effect.parts.ParticleEmitter data)
    {
        var curProgress = parent.Entity.Get<components.effect.CombinerPlayback>().CurProgress;

        var spawnRate = 0f;
        switch(data.spawnMode)
        {
            case zzio.effect.parts.ParticleSpawnMode.Constant:
                spawnRate = data.spawnRate;
                break;
            case zzio.effect.parts.ParticleSpawnMode.Loadup:
                if (curProgress > data.minProgress && state.CurPhase1 > 0f)
                {
                    state.CurPhase1 -= elapsedTime;
                    spawnRate = data.spawnRate;
                }
                break;
            case zzio.effect.parts.ParticleSpawnMode.Normal:
                if (curProgress < data.minProgress)
                    break;
                if (state.CurPhase1 > 0f)
                {
                    state.CurPhase1 -= elapsedTime;
                    spawnRate = data.spawnRate;
                }
                else if (state.CurPhase2 > 0f)
                {
                    state.CurPhase2 -= elapsedTime;
                    spawnRate = state.CurPhase2 / (data.phase2 / 1000f) * data.spawnRate;
                }
                break;
            case zzio.effect.parts.ParticleSpawnMode.Explosion:
                if (curProgress > data.minProgress)
                    spawnRate = data.spawnRate;
                break;
        }

        state.SpawnProgress += elapsedTime * spawnRate;
        state.SpawnsLeft = (int)state.SpawnProgress;
        state.SpawnProgress -= state.SpawnsLeft;
    }

    private void UpdateParticle(
        float elapsedTime,
        ref components.effect.EmitterState.Particle p)
    {
        if (p.Life < 0f)
            return;
        p.Life += elapsedTime;
        if (p.Life > p.MaxLife)
        {
            p.Life = -1f; // is dead
            return;
        }

        p.PrevPos = p.Pos;
        p.Pos += p.Velocity * elapsedTime;
        p.Velocity += (p.Acceleration + p.Gravity * GravityFactor) * elapsedTime;
        p.Gravity += p.GravityMod * elapsedTime;
        p.Scale += p.ScaleMod * elapsedTime;
        p.Color = Vector4.Clamp(p.Color + p.ColorMod * elapsedTime, Vector4.Zero, Vector4.One);
    }

    public static void SpawnBasicParticle(
        Random random,
        zzio.effect.parts.ParticleEmitter data,
        out Vector3 dir,
        ref components.effect.EmitterState.Particle p)
    {
        p.Life = 0f;
        p.MaxLife = Math.Clamp(random.In(data.life), 0.1f, 15f);

        p.Color = Vector4.Clamp(new(
            random.In(data.colorR),
            random.In(data.colorG),
            random.In(data.colorB),
            random.In(data.colorA)),
            Vector4.Zero, Vector4.One);

        p.ColorMod = new(
            data.colorR.mod + random.InLine() * data.colorR.width,
            data.colorG.mod + random.InLine() * data.colorG.width,
            data.colorB.mod + random.InLine() * data.colorB.width,
            data.colorA.mod);
        p.ColorMod = (p.ColorMod - p.Color) / p.MaxLife;

        p.Gravity = Vector3.Clamp(data.gravity, Vector3.One * -0.5f, Vector3.One * +0.5f) * GravityFactor;
        p.GravityMod = data.gravityMod * GravityFactor - p.Gravity / p.MaxLife;

        float horRot = random.InLine() * MathF.PI * 2f;
        float verRot = random.InLine() * MathF.PI * data.verticalDir;
        dir = (data.hasDirection ? Vector3.UnitZ : Vector3.Zero) + new Vector3(
            MathF.Cos(horRot) * MathF.Sin(verRot),
            MathF.Cos(verRot),
            MathF.Sin(horRot) * MathF.Sin(verRot));

        p.Velocity = dir * (data.minVel + random.InLine() * data.acc.width);

        p.Scale = 1f;
        p.ScaleMod = 0f;
    }

    public static void SpawnParticleScale(
        Random random,
        zzio.effect.parts.ParticleEmitter data,
        ref components.effect.EmitterState.Particle p)
    {
        p.Scale = random.In(data.scale);
        p.ScaleMod = (data.scale.mod - p.Scale) / p.MaxLife;
    }
}
