﻿using System;
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

    private readonly ITagContainer diContainer;
    private readonly IDisposable sceneLoadedSubscription;
    private readonly components.RenderOrder responsibility;

    private readonly ModelInstanceBuffer instanceBuffer;
    private readonly List<ClumpCount> clumpCounts = new();

    public ModelRenderer(ITagContainer diContainer, components.RenderOrder responsibility) :
        base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        this.diContainer = diContainer;
        this.responsibility = responsibility;
        instanceBuffer = new(diContainer);
        sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
    }

    public override void Dispose()
    {
        base.Dispose();
        sceneLoadedSubscription.Dispose();
        instanceBuffer.Dispose();
    }

    private void HandleSceneLoaded(in messages.SceneLoaded message)
    {
        clumpCounts.EnsureCapacity(MultiMap.Keys.Count());
        instanceBuffer.Ensure(MultiMap.Keys.Sum(MultiMap.Count) + 1); // remove hack to fix NpcMarker crash
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

        instanceBuffer.Add(new()
        {
            tint = materialInfo.Color,
            world = location.LocalToWorld,
            texShift = entity.TryGet<components.TexShift>().GetValueOrDefault(components.TexShift.Default).Matrix,
            vertexColorFactor = 0f,
            tintFactor = 1f,
            alphaReference = 0.082352944f
        });
    }

    protected override void PostUpdate(CommandList cl)
    {
        if (instanceBuffer.Count == 0)
            return;
        cl.PushDebugGroup($"{nameof(ModelRenderer)} {responsibility}");

        instanceBuffer.Update(cl);

        bool isFirstDraw;
        var curInstanceStart = 0u;
        foreach (var clumpCount in clumpCounts)
        {
            var (clump, materials, count) = (clumpCount.Clump, clumpCount.Materials, clumpCount.Count);
            cl.PushDebugGroup(clump.Name);

            isFirstDraw = true;
            foreach (var (subMesh, material) in clump.SubMeshes.Zip(materials))
            {
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
        instanceBuffer.Clear();
    }
}
