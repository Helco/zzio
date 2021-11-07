using System;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.effect;
using zzio.effect.parts;
using zzre.materials;

namespace zzre.rendering.effectparts
{
    public class ParticleBehaviourSpark : ListDisposable, IParticleBehaviour
    {
        private struct Spark
        {
            public BasicParticle basic;
            
            public void Update (float timeDelta, in ParticleEmitter data) => basic.Update(timeDelta);

            public void Spawn(Random random, Vector3 pos, in ParticleEmitter data) =>
                basic.SpawnLifeGravityColorDirVel(random, data, out var dir);
        }

        private readonly Random random = new();
        private readonly Location location;
        private readonly IQuadMeshBuffer<SparkVertex> quadMeshBuffer;
        private readonly DeviceBuffer instanceBuffer;
        private readonly SparkMaterial material;
        private readonly ParticleEmitter data;
        private readonly Range quadRange;
        private readonly Spark[] sparks;
        private readonly SparkInstance[] sparkInstances;

        public float SpawnRate { get; set; }
        public int CurrentParticles { get; private set; } = 0;
        public IEffectPart Part => data;

        private bool areInstancesDirty = true;
        private float spawnProgress = 0f;

        public ParticleBehaviourSpark(ITagContainer diContainer, Location location, ParticleEmitter data)
        {
            this.data = data;
            this.location = location;
            var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
            var camera = diContainer.GetTag<Camera>();
            quadMeshBuffer = diContainer.GetTag<IQuadMeshBuffer<SparkVertex>>();
            material = SparkMaterial.CreateFor(data.renderMode, true, diContainer);
            material.LinkTransformsTo(camera);
            material.World.Value = Matrix4x4.Identity;
            material.Uniforms.Value = SparkUniforms.Default;
            material.MainTexture.Texture = textureLoader.LoadTexture(IEffectPartRenderer.TexturePath, data.texName);
            material.Sampler.Value = SamplerAddressMode.Clamp.AsDescription(SamplerFilter.MinLinear_MagLinear_MipLinear);
            AddDisposable(material);

            sparks = new Spark[(int)(data.spawnRate * data.life.value)];
            sparkInstances = new SparkInstance[sparks.Length];
            quadRange = quadMeshBuffer.Reserve(sparks.Length);
            uint instanceBufferSize = (uint)sparks.Length * SparkInstance.Stride;
            instanceBuffer = diContainer.GetTag<ResourceFactory>()
                .CreateBuffer(new(instanceBufferSize, BufferUsage.VertexBuffer));
            AddDisposable(instanceBuffer);

            // scale and uv keep the same, so we set it once for all
            var width = data.scale.mod * 8f; // as per zanthp, maybe structure was an union all along...
            var height = data.scale.value;
            var tile = EffectPartUtility.GetTileUV(data.tileW, data.tileH, data.tileId);
            var quad = new SparkVertex[]
            {
                new() { pos = new(-width, -height), tex = new(tile.Min.X, tile.Min.Y) },
                new() { pos = new(-width, +height), tex = new(tile.Min.X, tile.Max.Y) },
                new() { pos = new(+width, +height), tex = new(tile.Max.X, tile.Max.Y) },
                new() { pos = new(+width, -height), tex = new(tile.Max.X, tile.Min.Y) },
            };
            for (var i = quadRange.Start; i.Value != quadRange.End.Value; i = i.Offset(1))
                quad.CopyTo(quadMeshBuffer[i]);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            quadMeshBuffer.Release(quadRange);
        }

        public void Reset()
        {
            foreach (ref var spark in sparks.AsSpan())
                spark.basic.life = -1f;
            areInstancesDirty = true;
        }

        public void AddTime(float deltaTime, float newProgress)
        {
            spawnProgress += SpawnRate * deltaTime;
            int spawnCount = (int)spawnProgress;
            spawnProgress -= MathF.Truncate(spawnProgress);

            foreach (ref var spark in sparks.AsSpan())
            {
                if (spark.basic.life >= 0f)
                    spark.Update(deltaTime, in data);
                else if (spawnCount > 0)
                {
                    spawnCount--;
                    spark.Spawn(random, location.GlobalPosition, in data);
                }
            }
            areInstancesDirty = true;
        }

        public void Render(CommandList cl)
        {
            if (areInstancesDirty)
            {
                areInstancesDirty = true;
                CurrentParticles = 0;
                foreach (ref readonly var spark in sparks.AsSpan())
                {
                    if (spark.basic.life >= 0f)
                        UpdateQuad(in spark, out sparkInstances[CurrentParticles++]);
                }
                uint instancesSize = (uint)CurrentParticles * SparkInstance.Stride;
                cl.UpdateBuffer(instanceBuffer, 0, ref sparkInstances[0], instancesSize);
            }

            var aliveRange = quadRange.Start..quadRange.Start.Offset(CurrentParticles);
            (material as IMaterial).Apply(cl);
            cl.SetVertexBuffer(1, instanceBuffer);
            quadMeshBuffer.Render(cl, aliveRange);
        }

        private void UpdateQuad(in Spark spark, out SparkInstance instance)
        {
            instance.center = spark.basic.pos;
            instance.dir = Vector3.Normalize(spark.basic.vel);
            instance.color = spark.basic.color;
        }
    }
}
