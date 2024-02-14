using System;
using DefaultEcs.System;
using Veldrid;
using zzre.materials;
using zzre.rendering;

namespace zzre.game.systems.effect;

public class EffectRenderer : AEntityMultiMapSystem<CommandList, EffectMaterial>
{
    private readonly EffectMesh effectMesh;
    private readonly RangeCollection indexRanges = [];
    private readonly components.RenderOrder responsibility;

    public EffectRenderer(ITagContainer diContainer, components.RenderOrder responsibility) :
        base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        effectMesh = diContainer.GetTag<EffectMesh>();
        this.responsibility = responsibility;
    }

    private static DefaultEcs.EntityMultiMap<EffectMaterial> CreateEntityContainer(object me, DefaultEcs.World world) => world
        .GetEntities()
        .With<EffectMaterial>()
        .With<components.effect.RenderIndices>()
        .With(static (in components.Visibility v) => v == components.Visibility.Visible)
        .With((in components.RenderOrder o) => o == (me as EffectRenderer)!.responsibility)
        .AsMultiMap<EffectMaterial>();

    protected override void PreUpdate(CommandList cl)
    {
        indexRanges.MaxRangeValue = effectMesh.IndexCapacity;
        cl.PushDebugGroup($"EffectRenderer {responsibility}");
        effectMesh.Update(cl);
        cl.SetIndexBuffer(effectMesh.IndexBuffer, DynamicMesh.IndexFormat);
    }

    protected override void Update(CommandList cl, in EffectMaterial key, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        var renderIndicesComponents = World.GetComponents<components.effect.RenderIndices>();
        var visibilityComponents = World.GetComponents<components.Visibility>();
        foreach (var entity in entities)
        {
            if (visibilityComponents[entity] == components.Visibility.Invisible)
                continue;
            var renderIndices = renderIndicesComponents[entity];
            indexRanges.Add(renderIndices.IndexRange);
        }
    }

    protected override void PostUpdate(CommandList cl, EffectMaterial material)
    {
        cl.PushDebugGroup($"{material.DebugName}");
        (material as IMaterial).Apply(cl);
        material.ApplyAttributes(cl, effectMesh);
        foreach (var range in indexRanges)
        {
            var (indexStart, indexCount) = range.GetOffsetAndLength(effectMesh.IndexCapacity);
            cl.DrawIndexed(
                indexStart: (uint)indexStart,
                indexCount: (uint)indexCount,
                instanceCount: 1,
                vertexOffset: 0,
                instanceStart: 0);
        }
        indexRanges.Clear();
        cl.PopDebugGroup();
    }

    protected override void PostUpdate(CommandList cl)
    {
        cl.PopDebugGroup();
    }
}
