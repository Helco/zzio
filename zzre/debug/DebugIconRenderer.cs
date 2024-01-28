using System;
using System.Linq;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;

namespace zzre.debug;

public class DebugIconRenderer : BaseDisposable
{
    private readonly DebugIconInstanceBuffer instanceBuffer;

    public UIMaterial Material { get; }

    public DebugIcon[] Icons
    {
        set
        {
            instanceBuffer.Clear();
            instanceBuffer.AddRange(value);
        }
    }

    public DebugIconRenderer(ITagContainer diContainer)
    {
        instanceBuffer = new(diContainer, dynamic: false);
        Material = new UIMaterial(diContainer) { IsInstanced = true, Is3D = true };
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        Material.Dispose();
        instanceBuffer.Dispose();
    }

    public void Render(CommandList cl)
    {
        if (instanceBuffer.VertexCount == 0)
            return;

        instanceBuffer.Update(cl);
        (Material as IMaterial).Apply(cl);
        Material.ApplyAttributes(cl, instanceBuffer);
        cl.Draw(
            vertexCount: 4,
            instanceCount: (uint)instanceBuffer.VertexCount,
            0, 0);
    }
}
