using System;
using System.Runtime.InteropServices;
using Veldrid;

namespace zzre.rendering
{
    public abstract class UniformBinding : BaseBinding
    {
        protected abstract uint SizeInBytes { get; }

        private DeviceBuffer? buffer; // if set, this buffer is owned
        private DeviceBufferRange? range = null;

        public bool OwnsBuffer => buffer != null;

        public DeviceBuffer Buffer
        {
            get
            {
                if (range?.Buffer != null)
                    return range?.Buffer!;
                if (buffer != null)
                    return buffer;
                buffer = Parent.Device.ResourceFactory.CreateBuffer(new BufferDescription(SizeInBytes, BufferUsage.UniformBuffer));
                range = null;
                return buffer;
            } 
            set
            {
                buffer?.Dispose();
                buffer = null;
                range = new DeviceBufferRange(value, 0, SizeInBytes);
                isBindingDirty = true;
            }
        }

        public DeviceBufferRange BufferRange
        {
            get => range ?? new DeviceBufferRange(Buffer, 0, SizeInBytes);
            set
            {
                if (buffer != value.Buffer)
                {
                    buffer?.Dispose();
                    buffer = null;
                    isBindingDirty = true;
                }
                range = value;
            }
        }

        protected void UseOwnBuffer()
        {
            if (!OwnsBuffer)
            {
                buffer = null;
                range = null;
            }
        }

        public override BindableResource? Resource => range as BindableResource ?? Buffer;

        public UniformBinding(IMaterial parent) : base(parent) { }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            buffer?.Dispose();
        }
    }

    public class UniformBinding<T> : UniformBinding where T : struct
    {
        protected override uint SizeInBytes => (uint)((Marshal.SizeOf<T>() + 15) / 16 * 16); // has to be aligned

        private T value = default;
        private bool isContentDirty = true;

        public ref T Ref
        {
            get
            {
                UseOwnBuffer();
                isContentDirty = true;
                return ref value;
            }
        }

        public T Value
        {
            get => value;
            set => Ref = value;
        }

        public UniformBinding(IMaterial material) : base(material) { }

        public void Update()
        {
            if (!isContentDirty || !OwnsBuffer)
                return;
            Parent.Device.UpdateBuffer(BufferRange.Buffer, BufferRange.Offset, ref value);
            isContentDirty = false;
        }

        public override void Update(CommandList cl)
        {
            if (!isContentDirty || !OwnsBuffer)
                return;
            cl.UpdateBuffer(BufferRange.Buffer, BufferRange.Offset, ref value);
            isContentDirty = false;
        }
    }
}
