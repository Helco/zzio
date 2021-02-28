using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio.utils;
using zzre;
using zzre.materials;
using zzre.rendering;

namespace zzmaps
{
    internal class TileSceneRenderData : ListDisposable
    {
        private static readonly FilePath[] TextureBasePaths = new[]
        {
            new FilePath("resources/textures/models"),
            new FilePath("resources/textures/worlds")
        };

        private readonly ITagContainer diContainer;
        private readonly LocationBuffer locationBuffer;
        private readonly TileScene scene;
        private readonly IReadOnlyList<IMaterial> worldMaterials;
        private readonly IReadOnlyList<IReadOnlyList<IMaterial>> objectMaterials;
        private readonly IReadOnlyList<DeviceBufferRange> locationRanges;

        public TileSceneRenderData(ITagContainer diContainer, TileScene scene)
        {
            this.diContainer = diContainer;
            this.scene = scene;
            locationBuffer = diContainer.GetTag<LocationBuffer>();
            var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
            var camera = diContainer.GetTag<OrthoCamera>();

            var worldLocationRange = locationBuffer.Add(new Location());
            var locationRanges = new List<DeviceBufferRange>(scene.Objects.Count + 1);
            this.locationRanges = locationRanges;
            worldMaterials = scene.WorldBuffers.Materials.Select(rwMaterial =>
            {
                var material = new ModelStandardMaterial(diContainer);
                (material.MainTexture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(TextureBasePaths, rwMaterial);
                material.Projection.BufferRange = camera.ProjectionRange;
                material.View.BufferRange = camera.ViewRange;
                material.World.Value = Matrix4x4.Identity;
                material.Uniforms.Ref = ModelStandardMaterialUniforms.Default;
                AddDisposable(material);
                return material;
            }).ToList();

            objectMaterials = scene.Objects.Select(obj => obj.ClumpBuffers.SubMeshes.Select(subMesh =>
            {
                var objectLocation = new Location();
                objectLocation.LocalPosition = obj.Position;
                objectLocation.LocalRotation = obj.Rotation;
                var objectLocationRange = locationBuffer.Add(objectLocation);

                var rwMaterial = subMesh.Material;
                var material = new ModelStandardMaterial(diContainer);
                (material.MainTexture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(TextureBasePaths, rwMaterial);
                material.Projection.BufferRange = camera.ProjectionRange;
                material.View.BufferRange = camera.ViewRange;
                material.World.BufferRange = objectLocationRange;
                material.Uniforms.Ref = ModelStandardMaterialUniforms.Default;
                material.Uniforms.Ref.vertexColorFactor = 0.0f;
                material.Uniforms.Ref.tint = rwMaterial.color.ToFColor() * obj.Tint;
                AddDisposable(material);
                return material;
            }).ToList()).ToList();

        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            foreach (var range in locationRanges)
                locationBuffer.Remove(range);
        }

        public void Render(CommandList cl)
        {
            scene.WorldBuffers.SetBuffers(cl);
            foreach (var group in scene.WorldBuffers.SubMeshes.GroupBy(subMesh => subMesh.MaterialIndex))
            {
                worldMaterials[group.Key].Apply(cl);
                foreach (var subMesh in group)
                    cl.DrawIndexed(
                        indexStart: (uint)subMesh.IndexOffset,
                        indexCount: (uint)subMesh.IndexCount,
                        instanceStart: 0,
                        instanceCount: 1,
                        vertexOffset: 0);
            }

            foreach (var (obj, objI) in scene.Objects.Indexed())
            {
                obj.ClumpBuffers.SetBuffers(cl);
                foreach (var (subMesh, subMeshI) in obj.ClumpBuffers.SubMeshes.Indexed())
                {
                    objectMaterials[objI][subMeshI].Apply(cl);
                    cl.DrawIndexed(
                        indexStart: (uint)subMesh.IndexOffset,
                        indexCount: (uint)subMesh.IndexCount,
                        instanceStart: 0,
                        instanceCount: 1,
                        vertexOffset: 0);
                }
            }
        }
    }
}