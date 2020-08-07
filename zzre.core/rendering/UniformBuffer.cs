using System;
using System.Runtime.InteropServices;
using Veldrid;

namespace zzre.rendering
{
    public class UniformBuffer<T> : BaseDisposable where T : struct
    {
        private T value = default;
        private bool isDirty = true;

        public ref T Ref
        {
            get
            {
                isDirty = true;
                return ref value;
            }
        }
        public T Value => value;
        public DeviceBuffer Buffer { get; }

        public UniformBuffer(ResourceFactory factory)
        {
            uint alignedSize = (uint)Marshal.SizeOf<T>();
            alignedSize = (alignedSize + 15) / 16 * 16;
            Buffer = factory.CreateBuffer(new BufferDescription(alignedSize, BufferUsage.UniformBuffer));
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            Buffer.Dispose();
        }

        public void SetIsDirty() => isDirty = true;

        public void Update(GraphicsDevice device)
        {
            if (!isDirty)
                return;
            device.UpdateBuffer(Buffer, 0, ref value);
            isDirty = false;
        }

        public void Update(CommandList cl)
        {
            if (!isDirty)
                return;
            cl.UpdateBuffer(Buffer, 0, ref value);
        }
    }
}
