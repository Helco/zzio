using System;
using System.Linq;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;

namespace zzre.debug
{
    public class DebugIconRenderer : BaseDisposable
    {
        private readonly ITagContainer diContainer;
        private DeviceBuffer? iconBuffer = null;
        private DebugIcon[] icons = Array.Empty<DebugIcon>();

        private int Capacity => (int)((iconBuffer?.SizeInBytes ?? 0) / DebugIcon.Stride);
        private uint Count => (uint)(icons?.Count() ?? 0);

        public DebugIconMaterial Material { get; }

        public bool IsDirty { get; set; } = true;

        public DebugIcon[] Icons
        {
            get => icons;
            set
            {
                icons = value;
                IsDirty = true;

                if (Capacity >= Count)
                    return;
                iconBuffer?.Dispose();
                iconBuffer = diContainer.GetTag<GraphicsDevice>().ResourceFactory.CreateBuffer(new BufferDescription(
                    Count * DebugIcon.Stride, BufferUsage.VertexBuffer));
                iconBuffer.Name = $"DebugIcon Instances {GetHashCode()}";
            }
        }

        public DebugIconRenderer(ITagContainer diContainer, int capacity = 1024)
        {
            this.diContainer = diContainer;
            Material = new DebugIconMaterial(diContainer);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            Material.Dispose();
            iconBuffer?.Dispose();
        }

        public void Render(CommandList cl, int iconStart = 0, int iconCount = -1)
        {
            if (iconBuffer == null)
                return;
            if (IsDirty)
            {
                IsDirty = false;
                cl.UpdateBuffer(iconBuffer, 0, icons);
            }
            if (iconCount < 0)
                iconCount = (int)Count;
            if (iconStart < 0 || iconStart + iconCount > Count)
                throw new ArgumentOutOfRangeException();

            (Material as IMaterial).Apply(cl);
            cl.SetVertexBuffer(0, iconBuffer);
            cl.Draw(4, (uint)iconCount, 0, (uint)iconStart);
        }
    }
}
