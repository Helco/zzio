using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.effect;
using zzio.effect.parts;
using zzio.vfs;
using zzre.rendering.effectparts;

namespace zzre.rendering
{
    public interface IEffectPartRenderer : IDisposable
    {
        static readonly FilePath TexturePath = new FilePath("Resources/Textures/Effects");
        static readonly SamplerDescription SamplerDescription =
            SamplerAddressMode.Clamp.AsDescription(SamplerFilter.MinLinear_MagLinear_MipLinear);

        IEffectPart Part { get; }

        void Render(CommandList cl);
        void Reset();
        void AddTime(float deltaTime, float newProgress);
    }

    public interface IEffectPartBeamRenderer : IEffectPartRenderer
    {
        float Length { get; set; }
    }

    public class EffectCombinerRenderer : ListDisposable
    {
        private readonly ITagContainer diContainer;
        private readonly LocationBuffer locationBuffer;
        private readonly DeviceBufferRange locationRange;
        private float length = 1f;

        public Location Location { get; } = new Location();
        public EffectCombiner Effect { get; }
        public IReadOnlyCollection<IEffectPartRenderer> Parts { get; }
        public float CurTime { get; private set; } = 0f;
        public float CurProgress { get; private set; } = 100f;

        public float Duration => Effect.Duration;
        public float SafeDuration => (Duration < float.Epsilon ? 1f : Duration);
        public float CurTimeNormalized => CurTime / SafeDuration;
        public bool IsLooping => Effect.isLooping;
        public bool IsDone => !IsLooping && CurTime >= Duration;

        public float Length
        {
            get => length;
            set
            {
                length = value;
                foreach (var beamPart in Parts.OfType<IEffectPartBeamRenderer>())
                    beamPart.Length = value;
            }
        }

        public EffectCombinerRenderer(ITagContainer diContainer, IResource resource)
            : this(diContainer, Load(resource)) { }
        private static EffectCombiner Load(IResource resource)
        {
            using var stream = resource.OpenContent() ??
                throw new IOException($"Could not open effect resource {resource.Path.ToPOSIXString()}");
            var effect = new EffectCombiner();
            effect.Read(stream);
            return effect;
        }

        public EffectCombinerRenderer(ITagContainer diContainer, EffectCombiner effect)
        {
            this.diContainer = diContainer.ExtendedWith(this);
            locationBuffer = diContainer.GetTag<LocationBuffer>();
            locationRange = locationBuffer.Add(Location);
            Effect = effect;
            Location.LocalPosition = effect.position;
            Location.LocalRotation = Quaternion.CreateFromRotationMatrix(
                Matrix4x4.CreateLookAt(Vector3.Zero, effect.forwards, effect.upwards));

            Parts = effect.parts.Select(part => part switch
                {
                    MovingPlanes mp => new MovingPlanesRenderer(diContainer, locationRange, mp),
                    RandomPlanes rp => new RandomPlanesRenderer(diContainer, locationRange, rp),
                    ParticleEmitter pe => new ParticleEmitterRenderer(diContainer, Location, pe),
                    BeamStar bs => new BeamStarRenderer(diContainer, locationRange, bs),

                    _ => new DummyRenderer(part) as IEffectPartRenderer // ignore it until we have an implementation for all supported in zzio
                    // _ => throw new NotSupportedException($"Unsupported effect combine part {part.GetType().Name}")
                }).Where(renderer => renderer != null).ToArray()!;
            foreach (var part in Parts)
                AddDisposable(part);
            Length = length;
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            locationBuffer.Remove(locationRange);
        }

        public void Render(CommandList cl)
        {
            foreach (var part in Parts)
                part.Render(cl);
        }

        public void Reset()
        {
            CurTime = 0f;
            CurProgress = 100f;
            foreach (var part in Parts)
                part.Reset();
        }

        public void AddTime(float timeDelta, float newProgress)
        {
            CurTime += timeDelta;
            if (IsLooping && CurTime > Duration)
                CurTime %= SafeDuration;
            if (!IsLooping && CurTime > Duration)
            {
                timeDelta -= CurTime - Duration;
                CurTime = Duration;
            }
            CurProgress = newProgress;
            foreach (var part in Parts)
                part.AddTime(timeDelta, newProgress);
        }

        private class DummyRenderer : IEffectPartRenderer
        {
            public IEffectPart Part { get; }
            public DummyRenderer(IEffectPart part) => Part = part;
            public float Duration => 0f;
            public void AddTime(float deltaTime, float newProgress) { }
            public void Dispose() { }
            public void Render(CommandList cl) { }
            public void Update() { }
            public void Reset() { }
        }
    }
}
