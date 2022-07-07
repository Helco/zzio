using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzre;
using zzre.materials;
using zzre.rendering;

namespace zzmaps
{
    internal class TileSceneRenderData : ListDisposable
    {
        private static readonly FilePath[] TextureBasePaths =
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

        public TileSceneRenderData(ITagContainer diContainer, TileScene scene, DeviceBuffer counterBuffer)
        {
            this.diContainer = diContainer;
            this.scene = scene;
            locationBuffer = diContainer.GetTag<LocationBuffer>();
            var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
            var camera = diContainer.GetTag<OrthoCamera>();

            var worldLocationRange = locationBuffer.Add(new Location());
            var locationRanges = new List<DeviceBufferRange>(scene.Objects.Count + 1) { worldLocationRange };
            this.locationRanges = locationRanges;
            worldMaterials = scene.WorldBuffers.Materials.Select(rwMaterial =>
            {
                IMapMaterial material;
                if (rwMaterial.isTextured)
                {
                    var texMaterial = new MapStandardMaterial(diContainer);
                    (texMaterial.MainTexture.Texture, texMaterial.Sampler.Sampler) = textureLoader.LoadTexture(TextureBasePaths, rwMaterial);
                    material = texMaterial;
                }
                else
                    material = new MapUntexturedMaterial(diContainer);
                material.Projection.BufferRange = camera.ProjectionRange;
                material.View.BufferRange = camera.ViewRange;
                material.World.Value = Matrix4x4.Identity;
                material.Uniforms.Ref = ModelStandardMaterialUniforms.Default;
                material.PixelCounter.Buffer = counterBuffer;
                AddDisposable(material);
                return material as IMaterial;
            }).ToList()!;

            objectMaterials = scene.Objects.Select(obj => obj.ClumpBuffers.SubMeshes.Select(subMesh =>
            {
                var objectLocation = new Location();
                objectLocation.LocalPosition = obj.Position;
                objectLocation.LocalRotation = obj.Rotation;
                var objectLocationRange = locationBuffer.Add(objectLocation);
                locationRanges.Add(objectLocationRange);

                var rwMaterial = subMesh.Material;
                IMapMaterial material;
                if (rwMaterial.isTextured)
                {
                    var texMaterial = new MapStandardMaterial(diContainer);
                    (texMaterial.MainTexture.Texture, texMaterial.Sampler.Sampler) = textureLoader.LoadTexture(TextureBasePaths, rwMaterial);
                    material = texMaterial;
                }
                else
                    material = new MapUntexturedMaterial(diContainer);
                material.Projection.BufferRange = camera.ProjectionRange;
                material.View.BufferRange = camera.ViewRange;
                material.World.BufferRange = objectLocationRange;
                material.Uniforms.Ref = ModelStandardMaterialUniforms.Default;
                material.Uniforms.Ref.vertexColorFactor = 0.0f;
                material.Uniforms.Ref.tint = rwMaterial.color.ToFColor() * obj.Tint;
                material.PixelCounter.Buffer = counterBuffer;
                AddDisposable(material);
                return material as IMaterial;
            }).ToList() as IReadOnlyList<IMaterial>).ToList();

        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            foreach (var range in locationRanges)
                locationBuffer.Remove(range);
        }

        public void Render(CommandList cl, Box visibleBox)
        {
            var visibleWorldMeshes = scene.WorldBuffers.Sections
                .OfType<WorldBuffers.MeshSection>()
                .Where(meshSection => meshSection.Bounds.Intersects(visibleBox))
                .SelectMany(meshSection => scene.WorldBuffers.SubMeshes.Range(meshSection.SubMeshes))
                .GroupBy(subMesh => subMesh.MaterialIndex);
            scene.WorldBuffers.SetBuffers(cl);
            foreach (var group in visibleWorldMeshes)
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