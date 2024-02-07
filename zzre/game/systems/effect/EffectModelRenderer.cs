using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using Veldrid;
using zzre.rendering;
using zzre.materials;
using zzio.effect.parts;

namespace zzre.game.systems.effect;

// This renderer could do better batching...

public partial class EffectModelRenderer : AEntitySetSystem<CommandList>
{
    private readonly components.RenderOrder responsibility;
    private readonly ModelInstanceBuffer instanceBuffer;

    public EffectModelRenderer(ITagContainer diContainer, components.RenderOrder responsibility)
        : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        this.responsibility = responsibility;
        instanceBuffer = diContainer.GetTag<ModelInstanceBuffer>();
    }

    [WithPredicate]
    private static bool IsVisible(in components.Visibility visibility) => visibility == components.Visibility.Visible;

    [WithPredicate]
    private bool IsMyResponsibility(in components.RenderOrder o) => o == responsibility;

    protected override void PreUpdate(CommandList cl)
    {
        cl.PushDebugGroup("EffectModelRenderer");
    }

    protected override void PostUpdate(CommandList cl)
    {
        cl.PopDebugGroup();
    }

    [Update]
    private void Update(
        CommandList cl,
        ClumpMesh mesh,
        List<ModelMaterial> materials,
        ModelInstanceBuffer.InstanceArena instanceArena)
    {
        if (instanceArena.InstanceCount == 0)
            return;
        cl.PushDebugGroup(mesh.Name);
        bool isFirstDraw = true;
        instanceBuffer.Update(cl);

        foreach (var (subMesh, material) in mesh.SubMeshes.Zip(materials))
        {
            (material as IMaterial).Apply(cl);
            if (isFirstDraw)
            {
                isFirstDraw = false;
                material.ApplyAttributes(cl, mesh, instanceBuffer);
                cl.SetIndexBuffer(mesh.IndexBuffer, mesh.IndexFormat);
            }
            cl.DrawIndexed(
                vertexOffset: 0,
                indexStart: (uint)subMesh.IndexOffset,
                indexCount: (uint)subMesh.IndexCount,
                instanceStart: instanceArena.InstanceStart,
                instanceCount: instanceArena.InstanceCount);
        }
        cl.PopDebugGroup();
    }
}
