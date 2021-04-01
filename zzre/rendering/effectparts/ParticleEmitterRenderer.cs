using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Veldrid;
using zzio.effect;
using zzio.effect.parts;

namespace zzre.rendering.effectparts
{
    public interface IParticleBehaviour : IEffectPartRenderer
    {
        float SpawnRate { get; set; }
        int CurrentParticles { get; }
    }

    public class ParticleEmitterRenderer : BaseDisposable, IEffectPartRenderer
    {
        private readonly ParticleEmitter data;
        private readonly IParticleBehaviour behaviour;
        private float curPhase1 = -1f, curPhase2 = -1f;

        public IEffectPart Part => data;
        public int CurrentParticles => behaviour.CurrentParticles;
        public int MaxParticles => (int)(data.spawnRate * data.life.value);

        public ParticleEmitterRenderer(ITagContainer diContainer, Location location, ParticleEmitter data)
        {
            this.data = data;
            behaviour = data.type switch
            {
                ParticleType.Particle => new ParticleBehaviourParticle(diContainer, location, data),
                ParticleType.Model => new ParticleBehaviourModel(diContainer, location, data),

                _ => new DummyBehaviour()
                //_ => throw new NotSupportedException($"Unsupported particle emitter type {data.type}")
            };

            Reset();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            behaviour.Dispose();
        }

        public void Reset()
        {
            curPhase1 = data.phase1 / 1000f;
            curPhase2 = data.phase2 / 1000f;
            behaviour.Reset();
        }

        public void AddTime(float deltaTime, float newProgress)
        {
            behaviour.SpawnRate = 0f;
            switch(data.spawnMode)
            {
                case ParticleSpawnMode.Constant:
                    behaviour.SpawnRate = data.spawnRate;
                    break;
                case ParticleSpawnMode.Loadup:
                    if (newProgress > data.minProgress && curPhase1 > 0f)
                    {
                        curPhase1 -= deltaTime;
                        behaviour.SpawnRate = data.spawnRate;
                    }
                    break;
                case ParticleSpawnMode.Normal:
                    if (newProgress < data.minProgress)
                        break;
                    if (curPhase1 > 0f)
                    {
                        curPhase1 -= deltaTime;
                        behaviour.SpawnRate = data.spawnRate;
                    }
                    if (curPhase2 > 0f)
                    {
                        curPhase2 -= deltaTime;
                        behaviour.SpawnRate = curPhase2 / (data.phase2 / 1000f) * data.spawnRate;
                    }
                    break;
                case ParticleSpawnMode.Explosion:
                    if (newProgress > data.minProgress)
                        behaviour.SpawnRate = data.spawnRate;
                    break;

                default: throw new NotSupportedException($"Unsupported particle spawn mode {data.spawnMode}");
            }

            behaviour.AddTime(deltaTime, newProgress);
        }

        public void Render(CommandList cl) => behaviour.Render(cl);

        private class DummyBehaviour : IParticleBehaviour
        {
            public IEffectPart Part => null!;
            public float SpawnRate { get; set; }
            public int CurrentParticles => 0;
            public void AddTime(float deltaTime, float newProgress) { }
            public void Dispose() { }
            public void Render(CommandList cl) { }
            public void Reset() { }
        }
    }
}
