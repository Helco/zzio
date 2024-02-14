using System.Runtime.InteropServices;
using Veldrid;
using zzio;

namespace zzre.rendering;

public class UniformBuffer<T> : BaseDisposable where T : unmanaged
{
    private T value;
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

    public UniformBuffer(ResourceFactory factory, bool dynamic = false)
    {
        uint alignedSize = (uint)Marshal.SizeOf<T>();
        alignedSize = (alignedSize + 15) / 16 * 16;
        Buffer = factory.CreateBuffer(new BufferDescription(alignedSize, BufferUsage.UniformBuffer |
            (dynamic ? BufferUsage.Dynamic : default)));
        Buffer.Name = $"{GetType().Name} {GetHashCode()}";
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
