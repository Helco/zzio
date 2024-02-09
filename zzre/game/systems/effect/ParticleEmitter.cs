using System;
using System.Buffers;
using System.Numerics;
using DefaultEcs.Resource;
using zzio;
using zzre.materials;

namespace zzre.game.systems.effect;

public sealed class ParticleEmitter : BaseCombinerPart<
    zzio.effect.parts.ParticleEmitter,
    components.effect.ParticleEmitterState>
{
    // a case of bad math: the original engine rotates (1,1,1) randomly, but this does not result in normalized vectors
    private static readonly float DistanceFactor = MathF.Sqrt(3f);

    private readonly MemoryPool<components.effect.ParticleEmitterState.Particle> particleMemoryPool;
    private readonly Random random = Random.Shared;

    public ParticleEmitter(ITagContainer diContainer) : base(diContainer)
    {
        particleMemoryPool = MemoryPool<components.effect.ParticleEmitterState.Particle>.Shared;
    }

    public override void Dispose()
    {
        base.Dispose();
        particleMemoryPool.Dispose();
    }

    protected override void HandleRemovedComponent(in DefaultEcs.Entity entity, in components.effect.ParticleEmitterState state)
    {
        effectMesh.ReturnVertices(state.VertexRange);
        effectMesh.ReturnIndices(state.IndexRange);
        state.ParticleMemoryOwner.Dispose();
    }

    protected override void HandleAddedComponent(in DefaultEcs.Entity entity, in zzio.effect.parts.ParticleEmitter data)
    {
        if (data.type != zzio.effect.parts.ParticleType.Particle)
            return;

        var playback = entity.Get<components.Parent>().Entity.Get<components.effect.CombinerPlayback>();
        int maxParticleCount = (int)(data.spawnRate * data.life.value);
        var particleMemoryOwner = particleMemoryPool.Rent(maxParticleCount);
        var vertexRange = effectMesh.RentVertices(maxParticleCount * 4);
        var indexRange = effectMesh.RentQuadIndices(vertexRange);
        entity.Set(new components.effect.ParticleEmitterState(
            particleMemoryOwner,
            maxParticleCount,
            vertexRange,
            indexRange));

        entity.Set(ManagedResource<EffectMaterial>.Create(new resources.EffectMaterialInfo(
            playback.DepthTest,
            EffectMaterial.BillboardMode.View,
            data.renderMode,
            data.texName)));
        entity.Set(new components.effect.RenderIndices(default));
    }

    protected override void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        in components.Parent parent,
        ref components.effect.ParticleEmitterState state,
        in zzio.effect.parts.ParticleEmitter data,
        ref components.effect.RenderIndices indices)
    {
        var location = parent.Entity.Get<Location>();
        ref var emitter = ref entity.Get<components.effect.EmitterState>();
        var basics = emitter.Particles.Span;
        var extras = state.Particles.Span;

        int aliveCount = 0;
        for(int i = 0; i < basics.Length; i++)
        {
            if (basics[i].Life > 0f)
            {
                extras[i].TileLife -= elapsedTime;
                if (extras[i].TileLife < 0f)
                {
                    extras[i].TileLife = data.tileDuration / 1000f;
                    extras[i].TileI = (extras[i].TileI + 1) % data.tileCount;
                }
            }
            else if (emitter.SpawnsLeft > 0)
            {
                emitter.SpawnsLeft--;
                SpawnParticle(location, data, ref basics[i], ref extras[i]);
            }
            else
                continue;

            UpdateQuad(state, data, basics[i], extras[i], aliveCount++);
        }

        indices = new(state.IndexRange.Sub(0..(aliveCount * 6), effectMesh.IndexCapacity));
    }

    private void SpawnParticle(
        Location location,
        in zzio.effect.parts.ParticleEmitter data,
        ref components.effect.EmitterState.Particle basic,
        ref components.effect.ParticleEmitterState.Particle extra)
    {
        Emitter.SpawnBasicParticle(random, data, out var dir, ref basic);
        Emitter.SpawnParticleScale(random, data, ref basic);

        basic.PrevPos = basic.Pos = location.GlobalPosition +
            random.OnSphere() * DistanceFactor * new Vector3(data.horRadius, data.verRadius, data.horRadius);

        basic.Acceleration = (dir * random.In(data.acc) - basic.Velocity) / basic.MaxLife;

        extra.TileI = 0;
        extra.TileLife = 0f;
    }

    private void UpdateQuad(
        in components.effect.ParticleEmitterState state,
        in zzio.effect.parts.ParticleEmitter data,
        in components.effect.EmitterState.Particle basic,
        in components.effect.ParticleEmitterState.Particle extra,
        int index)
    {
        var size = basic.Scale * 0.5f;
        var right = Vector3.UnitX * size;
        var up = Vector3.UnitY * size;
        var texCoords = EffectMesh.GetTileUV(data.tileW, data.tileH, (data.tileId + extra.TileI));
        effectMesh.SetQuad(state.VertexRange, index * 4, applyCenter: false,
            basic.Pos, right, up, basic.Color.ToFColor(), texCoords);
    }
}
