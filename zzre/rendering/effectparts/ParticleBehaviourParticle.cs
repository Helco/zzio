using System;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.effect;
using zzio.effect.parts;
using zzre.materials;

namespace zzre.rendering.effectparts
{
    public class ParticleBehaviourParticle : ListDisposable, IParticleBehaviour
    {
        private struct Particle
        {
            public BasicParticle basic;
            public int tileI;
            public float tileLife;

            public void Update(float timeDelta, in ParticleEmitter data)
            {
                basic.Update(timeDelta);
                tileLife -= timeDelta;
                if (tileLife < 0f)
                {
                    tileLife = data.tileDuration / 1000f;
                    tileI = (tileI + 1) % (int)data.tileCount;
                }
            }

            public void Spawn(Random random, Vector3 pos, in ParticleEmitter data)
            {
                tileI = 0;
                tileLife = 0f;

                basic.SpawnLifeGravityColorDirVel(random, in data, out var dir);
                basic.SpawnScale(random, in data);

                basic.pos = pos + 
                    Vector3.Multiply(random.InCube(), new Vector3(data.horRadius, data.verRadius, data.horRadius));
                basic.prevPos = basic.pos;

                basic.acc = (dir * random.In(data.acc) - basic.vel) / basic.maxLife;
            }
        }

        private readonly Random random = new Random();
        private readonly Location location;
        private readonly IQuadMeshBuffer<EffectVertex> quadMeshBuffer;
        private readonly EffectMaterial material;
        private readonly ParticleEmitter data;
        private readonly Range quadRange;
        private readonly Rect[] tileTexCoords;
        private readonly Particle[] particles;

        public float SpawnRate { get; set; }
        public IEffectPart Part => data;
        public int CurrentParticles => aliveRange.End.Value - aliveRange.Start.Value;

        private Range aliveRange;
        private bool areQuadsDirty = true;
        private float spawnProgress = 0f;

        public ParticleBehaviourParticle(ITagContainer diContainer, Location location, ParticleEmitter data)
        {
            this.data = data;
            this.location = location;
            var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
            var camera = diContainer.GetTag<Camera>();
            quadMeshBuffer = diContainer.GetTag<IQuadMeshBuffer<EffectVertex>>();
            material = EffectMaterial.CreateFor(data.renderMode, diContainer);
            material.LinkTransformsTo(camera);
            material.World.Value = Matrix4x4.Identity; // particles are spawned in world-space
            material.Uniforms.Value = EffectMaterialUniforms.Default;
            material.MainTexture.Texture = textureLoader.LoadTexture(IEffectPartRenderer.TexturePath, data.texName);
            material.Sampler.Value = SamplerAddressMode.Clamp.AsDescription(SamplerFilter.MinLinear_MagLinear_MipLinear);
            AddDisposable(material);

            particles = new Particle[(int)(data.spawnRate * data.life.value)];
            quadRange = quadMeshBuffer.Reserve(particles.Length);
            tileTexCoords = EffectPartUtility.GetTileUV(data.tileW, data.tileH, data.tileId, data.tileCount);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            quadMeshBuffer.Release(quadRange);
        }

        public void Reset()
        {
            foreach (ref var particle in particles.AsSpan())
                particle.basic.life = -1f;
            areQuadsDirty = true;
        }

        public void AddTime(float deltaTime, float newProgress)
        {
            deltaTime = Math.Min(0.06f, deltaTime);
            spawnProgress += SpawnRate * deltaTime;
            int spawnCount = (int)spawnProgress;
            spawnProgress -= MathF.Truncate(spawnProgress);

            foreach (ref var particle in particles.AsSpan())
            {
                if (particle.basic.life >= 0f)
                    particle.Update(deltaTime, in data);
                else if (spawnCount > 0)
                {
                    spawnCount--;
                    particle.Spawn(random, location.GlobalPosition, in data);
                }
            }
            areQuadsDirty = true;
        }

        public void Render(CommandList cl)
        {
            if (areQuadsDirty)
            {
                areQuadsDirty = false;
                var count = 0;
                foreach (ref readonly var particle in particles.AsSpan())
                {
                    if (particle.basic.life >= 0f)
                        UpdateQuad(in particle, quadRange.Start.Offset(count++));
                }
                aliveRange = quadRange.Start..quadRange.Start.Offset(count);
            }

            (material as IMaterial).Apply(cl);
            quadMeshBuffer.Render(cl, aliveRange);
        }

        private void UpdateQuad(in Particle particle, Index index)
        {
            var size = MathF.Max(data.scale.value, particle.basic.scale);
            var right = Vector3.UnitX * size;
            var up = Vector3.UnitY * size;
            var texCoords = tileTexCoords[particle.tileI];

            quadMeshBuffer[index].UpdateQuad(particle.basic.pos, right, up, particle.basic.color, texCoords);
        }
    }
}
