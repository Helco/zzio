using System;
using System.Collections.Generic;
using System.Linq;
using Veldrid;

namespace zzre.rendering
{
    public class BaseMaterial : BaseDisposable, IMaterial
    {
        private class BindingSet : BaseDisposable
        {
            private readonly ResourceFactory factory;
            private readonly ResourceLayout layout;
            private readonly uint index;
            private ResourceSet? resourceSet;

            public List<BaseBinding> Bindings { get; } = new List<BaseBinding>();

            public BindingSet(ResourceFactory factory, ResourceLayout layout, uint index)
            {
                this.factory = factory;
                this.layout = layout;
                this.index = index;
            }

            protected override void DisposeManaged()
            {
                base.DisposeManaged();
                foreach (var binding in Bindings)
                    binding.Dispose();
                resourceSet?.Dispose();
            }

            public void Apply(CommandList cl)
            {
                bool isDirty = false;
                foreach (var binding in Bindings)
                {
                    binding.Update(cl);
                    isDirty |= binding.ResetIsDirty();
                }
                if (isDirty || resourceSet == null)
                {
                    resourceSet?.Dispose();
                    resourceSet = factory.CreateResourceSet(new ResourceSetDescription()
                    {
                        Layout = layout,
                        BoundResources = Bindings.Select(b => b.Resource).ToArray()
                    });
                }
                cl.SetGraphicsResourceSet(index, resourceSet);
            }
        }

        private readonly BindingSet[] bindingSets;
        public GraphicsDevice Device { get; }
        public IBuiltPipeline Pipeline { get; }
        public IEnumerable<BaseBinding> Bindings => bindingSets.SelectMany(set => set.Bindings);

        protected BaseMaterial(GraphicsDevice device, IBuiltPipeline pipeline)
        {
            Device = device;
            Pipeline = pipeline;
            bindingSets = Enumerable.Range(0, pipeline.ResourceLayouts.Count)
                .Select(i => new BindingSet(Device.ResourceFactory, pipeline.ResourceLayouts[i], (uint)i))
                .ToArray();
        }

        public void ApplyBindings(CommandList cl)
        {
            foreach (var bindingSet in bindingSets)
                bindingSet.Apply(cl);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            foreach (var set in bindingSets)
                set.Dispose();
        }

        protected Configurator Configure() => new Configurator(this);

        protected class Configurator
        {
            private readonly BaseMaterial parent;
            private int curSetI = 0;

            public Configurator(BaseMaterial parent)
            {
                this.parent = parent;
            }

            public Configurator NextBindingSet()
            {
                curSetI++;
                return this;
            }

            public Configurator Add(BaseBinding binding)
            {
                if (curSetI >= parent.bindingSets.Length)
                    throw new InvalidOperationException("Invalid binding set configuration, too many sets");
                parent.bindingSets[curSetI].Bindings.Add(binding);
                return this;
            }
        }
    }
}
