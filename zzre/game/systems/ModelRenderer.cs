using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using Veldrid;
using zzio;
using zzre.rendering;
using zzre.materials;
using zzio.scn;
using DefaultEcs;

namespace zzre.game.systems
{
    [With(typeof(components.Visibility))]
    public partial class ModelRenderer : AEntityMultiMapSystem<CommandList, ClumpBuffers>
    {
        private struct ClumpCount
        {
            public readonly ClumpBuffers Clump;
            public readonly IReadOnlyList<BaseModelInstancedMaterial> Materials;
            public readonly uint Count;

            public ClumpCount(ClumpBuffers clump, IReadOnlyList<BaseModelInstancedMaterial> materials, uint count = 1)
            {
                Clump = clump;
                Materials = materials;
                Count = count;
            }

            public ClumpCount Increment() => new ClumpCount(Clump, Materials, Count + 1);
        }

        private readonly ITagContainer diContainer;
        private readonly IDisposable sceneLoadedSubscription;
        private readonly components.RenderOrder responsibility;

        private readonly List<ClumpCount> clumpCounts = new List<ClumpCount>();
        private readonly List<ModelInstance> instances = new List<ModelInstance>();
        private DeviceBuffer? instanceBuffer; // not owned
        private uint instanceStart;

        public ModelRenderer(ITagContainer diContainer, components.RenderOrder responsibility) :
            base(diContainer.GetTag<World>(), CreateEntityContainer, null, 0)
        {
            this.diContainer = diContainer;
            this.responsibility = responsibility;
            sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        }

        public override void Dispose()
        {
            base.Dispose();
            sceneLoadedSubscription.Dispose();
        }

        // TODO: Remove workaround after Doraku/DefaultEcs.Analyzer#7
        private static EntityMultiMap<ClumpBuffers> CreateEntityContainer(object sender, World world) => world
            .GetEntities()
            .With<List<BaseModelInstancedMaterial>>()
            .With<Location>()
            .With<components.ClumpMaterialInfo>()
            .With<components.Visibility>()
            .With<components.RenderOrder>(((ModelRenderer)sender).Filter)
            .AsMultiMap(sender as IEqualityComparer<ClumpBuffers>);

        protected override void Update(CommandList cl, in ClumpBuffers key, in Entity entity)
        {
            var materials = entity.Get<List<BaseModelInstancedMaterial>>();
            var location = entity.Get<Location>();
            ref readonly var materialInfo = ref entity.Get<components.ClumpMaterialInfo>();
            Update(cl, key, materials, location, materialInfo);
        }

        private void HandleSceneLoaded(in messages.SceneLoaded message)
        {
            // only get the tag now, as it was only just created for us
            var modelInstanceBuffer = diContainer.GetTag<ModelInstanceBuffer>();
            var totalCount = MultiMap.Keys.Sum(key => MultiMap.Count(key));
            instanceBuffer = modelInstanceBuffer.DeviceBuffer;
            instanceStart = modelInstanceBuffer.Reserve(totalCount);
            clumpCounts.Capacity = MultiMap.Keys.Count();
            instances.Capacity = totalCount;
        }

        [WithPredicate]
        private bool Filter(in components.RenderOrder order) => order == responsibility;

        protected override void PreUpdate(CommandList state)
        {
            clumpCounts.Clear();
            instances.Clear();
        }

        private void Update(
            CommandList cl,
            in ClumpBuffers clumpBuffers,
            List<BaseModelInstancedMaterial> materials,
            Location location,
            in components.ClumpMaterialInfo materialInfo)
        {
            if (clumpCounts.LastOrDefault().Clump != clumpBuffers)
                clumpCounts.Add(new(clumpBuffers, materials));
            else
                clumpCounts[^1] = clumpCounts[^1].Increment();

            instances.Add(new()
            {
                tint = materialInfo.Color,
                world = location.LocalToWorld
            });
        }

        protected override void PostUpdate(CommandList cl)
        {
            if (instanceBuffer == null)
                throw new InvalidOperationException("Model instance buffer was never set");

            var instanceSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(instances);
            cl.UpdateBuffer(instanceBuffer,
                ModelInstance.Stride * instanceStart,
                ref instanceSpan[0],
                ModelInstance.Stride * (uint)instances.Count);

            bool isFirstDraw;
            bool isFirstClump = true;
            var curInstanceStart = instanceStart;
            foreach (var clumpCount in clumpCounts)
            {
                var (clump, materials, count) = (clumpCount.Clump, clumpCount.Materials, clumpCount.Count);

                isFirstDraw = true;
                foreach (var (subMesh, material) in clump.SubMeshes.Zip(materials))
                {
                    (material as IMaterial).Apply(cl);
                    if (isFirstDraw)
                    {
                        isFirstDraw = false;
                        clump.SetBuffers(cl);
                    }
                    if (isFirstClump)
                    {
                        isFirstClump = false;
                        cl.SetVertexBuffer(1, instanceBuffer);
                    }
                    cl.DrawIndexed(
                        vertexOffset: 0,
                        indexStart: (uint)subMesh.IndexOffset,
                        indexCount: (uint)subMesh.IndexCount,
                        instanceStart: curInstanceStart,
                        instanceCount: count);
                }

                curInstanceStart += count;
            }
        }
    }
}
