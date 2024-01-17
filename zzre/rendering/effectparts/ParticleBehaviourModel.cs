using System;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.effect;
using zzio.effect.parts;
using zzre.materials;

namespace zzre.rendering.effectparts;

public class ParticleBehaviourModel : ListDisposable, IParticleBehaviour
{
    private struct Model
    {
        public BasicParticle basic;
        public Vector3 rotationAxis;
        public float rotation, rotationSpeed;

        public void Update(float timeDelta)
        {
            basic.Update(timeDelta);
            rotation += rotationSpeed * timeDelta;
        }

        public void Spawn(Random random, Vector3 pos, in ParticleEmitter data)
        {
            basic.SpawnLifeGravityColorDirVel(random, in data, out var dir);
            basic.SpawnScale(random, in data);

            basic.pos = basic.prevPos = pos;
            basic.acc = (dir * random.In(data.acc) - basic.vel) / basic.maxLife;

            rotationAxis = random.InSphere();
            rotationSpeed = (random.InLine() * data.minVel + 1.0f) * 180.0f;
            rotation = random.InLine() * 180f;
        }
    }

    private readonly Random random = new();
    private readonly Location location;
    private readonly ClumpMesh clumpMesh;
    private readonly ModelMaterial[] materials;
    private readonly ParticleEmitter data;
    private readonly Model[] models;
    private readonly ModelInstanceBuffer instanceBuffer;

    public float SpawnRate { get; set; }
    public int CurrentParticles => instanceBuffer.Count;
    public IEffectPart Part => data;

    private bool areInstancesDirty = true;
    private float spawnProgress = 0f;

    public ParticleBehaviourModel(ITagContainer diContainer, Location location, ParticleEmitter data)
    {
        this.data = data;
        this.location = location;
        var clumpLoader = diContainer.GetTag<IAssetLoader<ClumpMesh>>();
        var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
        var camera = diContainer.GetTag<Camera>();

        var clumpPath = new FilePath("resources/models/models").Combine(data.texName + ".dff");
        clumpMesh = clumpLoader.Load(clumpPath);
        materials = clumpMesh.Materials.Select(rwMaterial =>
        {
            var (texture, sampler) = textureLoader.LoadTexture(new[]
            {
                IEffectPartRenderer.TexturePath,
                new FilePath("resources/textures/models")
            }, rwMaterial);
            var material = new ModelMaterial(diContainer)
            {
                IsInstanced = true,
                Blend = data.renderMode switch
                {
                    EffectPartRenderMode.NormalBlend => ModelMaterial.BlendMode.Alpha,
                    EffectPartRenderMode.Additive => ModelMaterial.BlendMode.Additive,
                    EffectPartRenderMode.AdditiveAlpha => ModelMaterial.BlendMode.AdditiveAlpha,
                    _ => throw new NotSupportedException($"Unsupported render mode for {nameof(ParticleBehaviourModel)}: {data.renderMode}")
                }
            };
            material.Texture.Texture = texture;
            material.Sampler.Sampler = sampler;
            material.Projection.BufferRange = camera.ProjectionRange;
            material.View.BufferRange = camera.ViewRange;
            AddDisposable(material);
            return material;
        }).ToArray();

        models = new Model[(int)(data.spawnRate * data.life.value)];
        instanceBuffer = new(diContainer);
        instanceBuffer.Ensure(models.Length);
        AddDisposable(instanceBuffer);
    }

    public void Reset()
    {
        foreach (ref var model in models.AsSpan())
            model.basic.life = -1f;
        areInstancesDirty = true;
    }

    public void AddTime(float deltaTime, float newProgress)
    {
        deltaTime = Math.Min(0.06f, deltaTime);
        spawnProgress += SpawnRate * deltaTime;
        int spawnCount = (int)spawnProgress;
        spawnProgress -= MathF.Truncate(spawnProgress);

        foreach (ref var model in models.AsSpan())
        {
            if (model.basic.life >= 0f)
                model.Update(deltaTime);
            else if (spawnCount > 0)
            {
                spawnCount--;
                model.Spawn(random, location.GlobalPosition, in data);
            }
        }
        areInstancesDirty = true;
    }

    public void Render(CommandList cl)
    {
        if (areInstancesDirty)
        {
            areInstancesDirty = false;
            instanceBuffer.Clear();
            foreach (ref readonly var model in models.AsSpan())
            {
                if (model.basic.life < 0f)
                    continue;
                instanceBuffer.Add(new()
                {
                    world =
                        Matrix4x4.CreateFromAxisAngle(model.rotationAxis, model.rotation * MathF.PI / 180f) *
                        Matrix4x4.CreateTranslation(model.basic.pos),
                    tint = model.basic.color.ToFColor(),
                    alphaReference = 0.03f,
                    vertexColorFactor = 0f,
                    tintFactor = 1f
                });
            }
            instanceBuffer.Update(cl);
        }

        materials.First().ApplyAttributes(cl, clumpMesh, instanceBuffer);
        cl.SetIndexBuffer(clumpMesh.IndexBuffer, clumpMesh.IndexFormat);
        foreach (var subMesh in clumpMesh.SubMeshes)
        {
            (materials[subMesh.Material] as IMaterial).Apply(cl);
            cl.DrawIndexed(
                indexStart: (uint)subMesh.IndexOffset,
                indexCount: (uint)subMesh.IndexCount,
                instanceStart: 0,
                instanceCount: (uint)CurrentParticles,
                vertexOffset: 0);
        }
    }
}
