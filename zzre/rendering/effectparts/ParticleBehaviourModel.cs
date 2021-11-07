using System;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.effect;
using zzio.effect.parts;
using zzio.utils;
using zzre.materials;

namespace zzre.rendering.effectparts
{
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

        private readonly Random random = new Random();
        private readonly Location location;
        private readonly ClumpBuffers clumpBuffers;
        private readonly DeviceBuffer instanceBuffer;
        private readonly BaseModelInstancedMaterial[] materials;
        private readonly ParticleEmitter data;
        private readonly Model[] models;
        private readonly ModelInstance[] modelInstances;

        public float SpawnRate { get; set; }
        public int CurrentParticles { get; private set; } = 0;
        public IEffectPart Part => data;

        private bool areInstancesDirty = true;
        private float spawnProgress = 0f;

        public ParticleBehaviourModel(ITagContainer diContainer, Location location, ParticleEmitter data)
        {
            this.data = data;
            this.location = location;
            var clumpLoader = diContainer.GetTag<IAssetLoader<ClumpBuffers>>();
            var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
            var camera = diContainer.GetTag<Camera>();

            var clumpPath = new FilePath("resources/models/models").Combine(data.texName + ".dff");
            clumpBuffers = clumpLoader.Load(clumpPath);
            materials = clumpBuffers.SubMeshes.Select(subMesh =>
            {
                var (texture, sampler) = textureLoader.LoadTexture(new[]
                {
                    IEffectPartRenderer.TexturePath,
                    new FilePath("resources/textures/models")
                }, subMesh.Material);
                var material = BaseModelInstancedMaterial.CreateFor(data.renderMode, diContainer);
                material.MainTexture.Texture = texture;
                material.Sampler.Sampler = sampler;
                material.Projection.BufferRange = camera.ProjectionRange;
                material.View.BufferRange = camera.ViewRange;
                material.Uniforms.Value = ModelInstancedUniforms.Default;
                material.Uniforms.Ref.vertexColorFactor = 0f;
                material.Uniforms.Ref.alphaReference = 0.03f;
                AddDisposable(material);
                return material;
            }).ToArray();

            models = new Model[(int)(data.spawnRate * data.life.value)];
            modelInstances = new ModelInstance[models.Length];
            uint instanceBufferSize = (uint)models.Length * ModelInstance.Stride;
            instanceBuffer = diContainer.GetTag<ResourceFactory>()
                .CreateBuffer(new BufferDescription(instanceBufferSize, BufferUsage.VertexBuffer));
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
                CurrentParticles = 0;
                foreach (ref readonly var model in models.AsSpan())
                {
                    if (model.basic.life < 0f)
                        continue;
                    ref var instance = ref modelInstances[CurrentParticles++];
                    instance.world =
                        Matrix4x4.CreateFromAxisAngle(model.rotationAxis, model.rotation * MathF.PI / 180f) *
                        Matrix4x4.CreateTranslation(model.basic.pos);
                    instance.tint = model.basic.color.ToFColor();
                }

                uint updateSize = (uint)CurrentParticles * ModelInstance.Stride;
                cl.UpdateBuffer(instanceBuffer, 0, ref modelInstances[0], updateSize);
            }

            clumpBuffers.SetBuffers(cl);
            foreach (var (subMesh, i) in clumpBuffers.SubMeshes.Indexed())
            {
                (materials[i] as IMaterial).Apply(cl);
                cl.SetVertexBuffer(1, instanceBuffer);
                cl.DrawIndexed(
                    indexStart: (uint)subMesh.IndexOffset,
                    indexCount: (uint)subMesh.IndexCount,
                    instanceStart: 0,
                    instanceCount: (uint)CurrentParticles,
                    vertexOffset: 0);
            }
        }
    }
}
