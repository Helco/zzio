using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using Veldrid;
using zzre.rendering;
using zzre.materials;

namespace zzre.game.systems;

[With(typeof(components.Visibility))]
public partial class ModelRenderer : AEntityMultiMapSystem<CommandList, ClumpMesh>
{
    private struct ClumpCount
    {
        public readonly ClumpMesh Clump;
        public readonly IReadOnlyList<ModelMaterial> Materials;
        public readonly uint Count;

        public ClumpCount(ClumpMesh clump, IReadOnlyList<ModelMaterial> materials, uint count = 1)
        {
            Clump = clump;
            Materials = materials;
            Count = count;
        }

        public ClumpCount Increment() => new(Clump, Materials, Count + 1);
    }

    private readonly IDisposable sceneChangingSubscription;
    private readonly IDisposable sceneLoadedSubscription;
    private readonly components.RenderOrder responsibility;
    private readonly ModelInstanceBuffer instanceBuffer;

    private readonly List<ClumpCount> clumpCounts = [];
    private ModelInstanceBuffer.InstanceArena? instanceArena;

    public ModelRenderer(ITagContainer diContainer, components.RenderOrder responsibility) :
        base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        this.responsibility = responsibility;
        instanceBuffer = diContainer.GetTag<ModelInstanceBuffer>();
        sceneChangingSubscription = World.Subscribe<messages.SceneChanging>(HandleSceneChanging);
        sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
    }

    public override void Dispose()
    {
        base.Dispose();
        sceneChangingSubscription.Dispose();
        sceneLoadedSubscription.Dispose();
    }

    private void HandleSceneChanging(in messages.SceneChanging message)
    {
        instanceArena?.Dispose();
        instanceArena = null;
    }

    private void HandleSceneLoaded(in messages.SceneLoaded message)
    {
        clumpCounts.EnsureCapacity(MultiMap.Keys.Count());
        var totalCount = MultiMap.Keys.Sum(MultiMap.Count);
        if (totalCount > 0)
            instanceArena = instanceBuffer.RentVertices(totalCount);
    }

    protected override void PreUpdate(CommandList cl)
    {
        // this allocates, MultiMap should just give the total count of entities instead
        int totalCount = MultiMap.Keys.Sum(MultiMap.Count);
        if ((instanceArena?.Capacity ?? 0) < totalCount)
        {
            instanceArena?.Dispose();
            instanceArena = instanceBuffer.RentVertices(totalCount);
        }
    }

    [WithPredicate]
    private bool Filter(in components.RenderOrder order) => order == responsibility;

    [Update]
    private void Update(
        CommandList _,
        in DefaultEcs.Entity entity,
        in ClumpMesh clumpMesh,
        List<ModelMaterial> materials,
        Location location,
        in components.ClumpMaterialInfo materialInfo)
    {
        if (clumpCounts.LastOrDefault().Clump != clumpMesh)
            clumpCounts.Add(new(clumpMesh, materials));
        else
            clumpCounts[^1] = clumpCounts[^1].Increment();

        instanceArena!.Add(new()
        {
            tint = materialInfo.Color,
            world = location.LocalToWorld,
            texShift = entity.TryGet<components.TexShift>().GetValueOrDefault(components.TexShift.Default).Matrix
        });
    }

    protected override void PostUpdate(CommandList cl)
    {
        if (instanceArena is null or { InstanceCount: 0 })
            return;
        cl.PushDebugGroup($"{nameof(ModelRenderer)} {responsibility}");

        instanceBuffer.Update(cl);

        bool isFirstDraw;
        var curInstanceStart = instanceArena.InstanceStart;
        foreach (var clumpCount in clumpCounts)
        {
            var (clump, materials, count) = (clumpCount.Clump, clumpCount.Materials, clumpCount.Count);
            cl.PushDebugGroup(clump.Name);

            isFirstDraw = true;
            for (int i = 0; i < clump.SubMeshes.Count; i++)
            {
                var material = materials[i];
                var subMesh = clump.SubMeshes[i];
                (material as IMaterial).Apply(cl);
                if (isFirstDraw)
                {
                    isFirstDraw = false;
                    material.ApplyAttributes(cl, clump, instanceBuffer);
                    cl.SetIndexBuffer(clump.IndexBuffer, clump.IndexFormat);
                }
                cl.DrawIndexed(
                    vertexOffset: 0,
                    indexStart: (uint)subMesh.IndexOffset,
                    indexCount: (uint)subMesh.IndexCount,
                    instanceStart: curInstanceStart,
                    instanceCount: count);
            }

            curInstanceStart += count;
            cl.PopDebugGroup();
        }
        cl.PopDebugGroup();

        for (int i = 0; i < clumpCounts.Count; i++)
            clumpCounts[i] = default; // remove reference to ClumpBuffer and materials
        clumpCounts.Clear();
        instanceArena.Reset();
    }
}
