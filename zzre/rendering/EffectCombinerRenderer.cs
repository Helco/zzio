using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Veldrid;
using zzio.effect;
using zzio.effect.parts;
using zzio.vfs;
using zzre.rendering.effectparts;

namespace zzre.rendering
{
    public interface IEffectCombinerPartRenderer : IDisposable
    {
        void Render(CommandList cl);
    }

    public class EffectCombinerRenderer : ListDisposable
    {
        private readonly ITagContainer diContainer;
        

        public Location Location { get; } = new Location();
        public EffectCombiner Effect { get; }
        public IReadOnlyCollection<IEffectCombinerPartRenderer> Parts { get; }

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
            this.Effect = effect;
            Location.LocalPosition = effect.position.ToNumerics();
            Location.LocalRotation = Quaternion.CreateFromRotationMatrix(
                Matrix4x4.CreateLookAt(Vector3.Zero, effect.forwards.ToNumerics(), effect.upwards.ToNumerics()));

            Parts = effect.parts.Select(part => part switch
                {
                    MovingPlanes mp => new MovingPlanesRenderer(diContainer, mp) as IEffectCombinerPartRenderer,

                    _ => null // ignore it until we have an implementation for all supported in zzio
                    // _ => throw new NotSupportedException($"Unsupported effect combine part {part.GetType().Name}")
                }).Where(renderer => renderer != null).ToArray()!;
            foreach (var part in Parts)
                AddDisposable(part);
        }

        public void Render(CommandList cl)
        {
            foreach (var part in Parts)
                part.Render(cl);
        }
    }
}
