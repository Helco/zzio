using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.Resource;
using zzre.materials;
using zzre.rendering;

namespace zzre.game.systems.effect;

public sealed class ModelEmitter : BaseCombinerPart<
    zzio.effect.parts.ParticleEmitter,
    components.effect.ModelEmitterState>
{
    private readonly MemoryPool<components.effect.ModelEmitterState.Particle> particleMemoryPool;
    private readonly ModelInstanceBuffer modelInstanceBuffer;
    private readonly Random random = Random.Shared;

    public ModelEmitter(ITagContainer diContainer) : base(diContainer)
    {
        particleMemoryPool = MemoryPool<components.effect.ModelEmitterState.Particle>.Shared;
        modelInstanceBuffer = diContainer.GetTag<ModelInstanceBuffer>();
    }

    public override void Dispose()
    {
        base.Dispose();
        particleMemoryPool.Dispose();
    }

    protected override void HandleRemovedComponent(in DefaultEcs.Entity entity, in components.effect.ModelEmitterState state)
    {
        state.ParticleMemoryOwner.Dispose();
        entity.Get<ModelInstanceBuffer.InstanceArena>().Dispose();
    }

    protected override void HandleAddedComponent(in DefaultEcs.Entity entity, in zzio.effect.parts.ParticleEmitter data)
    {
        if (data.particleType != zzio.effect.parts.ParticleType.Model)
            return;

        var playback = entity.Get<components.Parent>().Entity.Get<components.effect.CombinerPlayback>();
        int maxParticleCount = (int)(data.spawnRate * data.life.value);
        var particleMemoryOwner = particleMemoryPool.Rent(maxParticleCount);
        entity.Set(new components.effect.ModelEmitterState(
            particleMemoryOwner,
            maxParticleCount));
        entity.Set(modelInstanceBuffer.RentVertices(maxParticleCount));

        entity.Set(ManagedResource<ClumpMesh>.Create(resources.ClumpInfo.Model(data.texName + ".dff")));
        var clumpMesh = entity.Get<ClumpMesh>();
        if (clumpMesh.IsEmpty)
            throw new InvalidOperationException("ModelEmitter has an empty model");

        entity.Set(new List<ModelMaterial>(clumpMesh.Materials.Count));
        var renderMode = data.renderMode;
        entity.Set(ManagedResource<ModelMaterial>.Create(clumpMesh.Materials
            .Select(rwMaterial => new resources.ClumpMaterialInfo(
                rwMaterial,
                renderMode,
                depthTest: playback.DepthTest))
            .ToArray()));
        entity.Set<components.effect.RenderIndices>();
    }

    protected override void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        in components.Parent parent,
        ref components.effect.ModelEmitterState state,
        in zzio.effect.parts.ParticleEmitter data,
        ref components.effect.RenderIndices _)
    {
        var location = parent.Entity.Get<Location>();
        ref var emitter = ref entity.Get<components.effect.EmitterState>();
        var instanceArena = entity.Get<ModelInstanceBuffer.InstanceArena>();
        var basics = emitter.Particles.Span;
        var extras = state.Particles.Span;

        instanceArena.Reset();
        for (int i = 0; i < basics.Length; i++)
        {
            if (basics[i].Life > 0f)
                extras[i].Rotation += elapsedTime * extras[i].RotationSpeed;
            else if (emitter.SpawnsLeft > 0)
            {
                emitter.SpawnsLeft--;
                SpawnParticle(location, data, ref basics[i], ref extras[i]);
            }
            else
                continue;

            UpdateInstance(instanceArena, basics[i], extras[i]);
        }
    }

    private void SpawnParticle(
        Location location,
        in zzio.effect.parts.ParticleEmitter data,
        ref components.effect.EmitterState.Particle basic,
        ref components.effect.ModelEmitterState.Particle extra)
    {
        Emitter.SpawnBasicParticle(random, data, out var dir, ref basic);
        Emitter.SpawnParticleScale(random, data, ref basic);

        basic.PrevPos = basic.Pos = location.GlobalPosition;
        basic.Acceleration = (dir * random.In(data.acc) - basic.Velocity) / basic.MaxLife;

        extra.RotationAxis = random.InSphere();
        extra.RotationSpeed = (random.InLine() * data.minVel + 1f) * MathF.PI;
        extra.Rotation = random.InLine() * MathF.PI;
    }

    private static void UpdateInstance(
        ModelInstanceBuffer.InstanceArena instanceArena,
        in components.effect.EmitterState.Particle basic,
        in components.effect.ModelEmitterState.Particle extra)
    {
        instanceArena.Add(new()
        {
            world =
                Matrix4x4.CreateScale(basic.Scale) *
                Matrix4x4.CreateFromAxisAngle(extra.RotationAxis, extra.Rotation) *
                Matrix4x4.CreateTranslation(basic.Pos),
            tint = basic.Color.ToFColor()
        });
    }
}
