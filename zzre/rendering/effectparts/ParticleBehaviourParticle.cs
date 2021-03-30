using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Veldrid;
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
                basic.life = 0f;
                basic.maxLife = Math.Clamp(random.In(data.life), 0.1f, 15f);

                basic.color = Vector4.Clamp(new Vector4(
                    random.In(data.colorR),
                    random.In(data.colorG),
                    random.In(data.colorB),
                    random.In(data.colorA)),
                    Vector4.Zero, Vector4.One);
                basic.colorMod = new Vector4(
                    data.colorR.mod + random.InLine() * data.colorR.width,
                    data.colorG.mod + random.InLine() * data.colorG.width,
                    data.colorB.mod + random.InLine() * data.colorB.width,
                    data.colorA.mod + random.InLine() * data.colorA.width)
                    - basic.color / basic.maxLife;

                basic.scale = random.In(data.scale);
                basic.scaleMod = (data.scale.mod - basic.scale) / basic.maxLife;

                basic.gravity = Vector3.Clamp(data.gravity.ToNumerics(), Vector3.One * -0.5f, Vector3.One * +0.5f) * 9.8f;
                basic.gravityMod = 9.8f * data.gravityMod.ToNumerics() - basic.gravity / basic.maxLife;

                basic.pos = pos + 
                    Vector3.Multiply(random.InCube(), new Vector3(data.horRadius, data.verRadius, data.horRadius));
                basic.prevPos = basic.pos;

                float horRot = random.InLine() * MathF.PI * 2f;
                float verRot = random.InLine() * MathF.PI * data.verticalDir;
                var dir = (data.hasDirection ? Vector3.UnitZ : Vector3.Zero) + new Vector3(
                    MathF.Cos(horRot) * MathF.Sin(verRot),
                    MathF.Cos(verRot),
                    MathF.Sin(horRot) * MathF.Sin(verRot));

                basic.vel = dir * (data.minVel + random.InLine() * data.acc.width);
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
            AddDisposable(material.MainTexture.Texture = textureLoader.LoadTexture(
                IEffectPartRenderer.TexturePath, data.texName));
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
