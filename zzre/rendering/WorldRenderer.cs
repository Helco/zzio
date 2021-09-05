using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zzio.rwbs;
using zzio.utils;
using zzre.materials;
using Veldrid;

namespace zzre.rendering
{
    public class WorldRenderer : BaseDisposable
    {
        private readonly ITagContainer diContainer;

        private WorldBuffers? worldBuffers;
        public WorldBuffers? WorldBuffers
        {
            get => worldBuffers;
            set
            {
                worldBuffers = value;
                LoadMaterials();
                visibleMeshSections.Clear();
            }
        }

        private readonly DeviceBufferRange locationRange;
        private readonly Camera camera;
        private Frustum viewFrustum;
        private ModelStandardMaterial[] materials = new ModelStandardMaterial[0];
        public IReadOnlyList<ModelStandardMaterial> Materials => materials;

        private List<WorldBuffers.MeshSection> visibleMeshSections = new List<WorldBuffers.MeshSection>();
        public IReadOnlyList<WorldBuffers.MeshSection> VisibleMeshSections => visibleMeshSections;

        public Location Location { get; } = new Location();
        public Frustum ViewFrustum => viewFrustum;

        public WorldRenderer(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            camera = diContainer.GetTag<Camera>();
            var locationBuffer = diContainer.GetTag<LocationBuffer>();
            locationRange = locationBuffer.Add(Location);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            diContainer.GetTag<LocationBuffer>().Remove(locationRange);
            foreach (var material in materials)
                material.Dispose();
        }

        private void LoadMaterials()
        {
            foreach (var material in materials)
                material.Dispose();
            if (worldBuffers == null)
                return;

            var textureBase = new FilePath("resources/textures/worlds");
            var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
            var camera = diContainer.GetTag<Camera>();

            materials = new ModelStandardMaterial[worldBuffers.Materials.Count];
            foreach (var (rwMaterial, index) in worldBuffers.Materials.Indexed())
            {
                var material = materials[index] = new ModelStandardMaterial(diContainer);
                (material.MainTexture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(textureBase, rwMaterial);
                material.LinkTransformsTo(camera);
                material.World.BufferRange = locationRange;
                material.Uniforms.Ref = ModelStandardMaterialUniforms.Default;
            }
        }

        public void UpdateVisibility()
        {
            if (worldBuffers == null)
                return;

            viewFrustum.Projection = camera.View * camera.Projection;
            visibleMeshSections.Clear();
            var sectionQueue = new Queue<WorldBuffers.BaseSection>();
            sectionQueue.Enqueue(worldBuffers.Sections.First());
            while (sectionQueue.Any())
            {
                var section = sectionQueue.Dequeue();
                if (section.IsMesh)
                {
                    visibleMeshSections.Add((WorldBuffers.MeshSection)section);
                    continue;
                }

                var plane = (WorldBuffers.PlaneSection)section;
                var intersection = viewFrustum.Intersects(new Plane(plane.PlaneType.AsNormal().ToNumerics(), plane.CenterValue));
                if (intersection.HasFlag(PlaneIntersections.Inside))
                    sectionQueue.Enqueue(plane.RightChild);
                if (intersection.HasFlag(PlaneIntersections.Outside))
                    sectionQueue.Enqueue(plane.LeftChild);
            }
        }

        public void Render(CommandList cl, IEnumerable<WorldBuffers.MeshSection>? sections = null)
        {
            if (worldBuffers == null)
                return;
            sections ??= visibleMeshSections;

            var visibleSubMeshes = sections
                .SelectMany(m => worldBuffers.SubMeshes.Range(m.SubMeshes))
                .GroupBy(s => s.MaterialIndex);
            foreach (var group in visibleSubMeshes)
            {
                (materials[group.Key] as IMaterial).Apply(cl);
                worldBuffers.SetBuffers(cl);
                foreach (var subMesh in group)
                {
                    cl.DrawIndexed(
                        indexStart: (uint)subMesh.IndexOffset,
                        indexCount: (uint)subMesh.IndexCount,
                        instanceCount: 1,
                        vertexOffset: 0,
                        instanceStart: 0);
                }
            }
        }

        public void RenderForceAll(CommandList cl) =>
            Render(cl, worldBuffers?.Sections.OfType<WorldBuffers.MeshSection>());
    }
}
