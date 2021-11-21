using System;
using System.Linq;
using DefaultEcs.System;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;

namespace zzre.game.systems
{
    [With(typeof(components.ActorPart))]
    [With(typeof(components.Visibility))]
    public partial class ActorRenderer : AEntitySetSystem<CommandList>
    {
        private static readonly FilePath BaseTexturePath = new FilePath("resources/textures/actorsex");

        private readonly ITagContainer diContainer;
        private readonly Camera camera;
        private readonly IAssetLoader<Texture> textureLoader;
        private readonly IDisposable addSubscription;
        private readonly IDisposable removeSubscription;

        public ActorRenderer(ITagContainer diContainer) :
            base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
        {
            this.diContainer = diContainer;
            camera = diContainer.GetTag<Camera>();
            textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
            addSubscription = World.SubscribeComponentAdded<components.ActorPart>(HandleAddedComponent);
            removeSubscription = World.SubscribeComponentRemoved<ModelSkinnedMaterial[]>(HandleRemovedComponent);
        }

        public override void Dispose()
        {
            base.Dispose();
            addSubscription.Dispose();
            removeSubscription.Dispose();
        }

        [Update]
        private void Update(CommandList cl,
            in ClumpBuffers clumpBuffers,
            in ModelSkinnedMaterial[] materials)
        {
            foreach (var (subMesh, material) in clumpBuffers.SubMeshes.Zip(materials))
            {
                if (material.Pose.Skeleton?.CurrentAnimation != null)
                    material.Pose.MarkPoseDirty();
                (material as IMaterial).Apply(cl);
                clumpBuffers.SetBuffers(cl);
                clumpBuffers.SetSkinBuffer(cl);
                cl.DrawIndexed(
                    indexStart: (uint)subMesh.IndexOffset,
                    indexCount: (uint)subMesh.IndexCount,
                    vertexOffset: 0,
                    instanceStart: 0,
                    instanceCount: 1);
            }
        }

        private void HandleAddedComponent(in DefaultEcs.Entity entity, in components.ActorPart value)
        {
            var rwMaterials = entity.Get<ClumpBuffers>().SubMeshes.Select(sm => sm.Material);
            var skeleton = entity.Get<Skeleton>();
            var syncedLocation = entity.Get<components.SyncedLocation>();
            var zzreMaterials = rwMaterials.Select(rwMaterial =>
            {
                var material = new ModelSkinnedMaterial(diContainer);
                (material.MainTexture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(BaseTexturePath, rwMaterial);
                material.Uniforms.Ref = ModelStandardMaterialUniforms.Default;
                material.Uniforms.Ref.vertexColorFactor = 0.0f;
                material.Uniforms.Ref.tint = rwMaterial.color.ToFColor();
                material.Pose.Skeleton = skeleton;
                material.LinkTransformsTo(camera);
                material.World.BufferRange = syncedLocation.BufferRange;
                return material;
            }).ToArray();
            entity.Set(zzreMaterials);
        }

        private void HandleRemovedComponent(in DefaultEcs.Entity entity, in ModelSkinnedMaterial[] materials)
        {
            foreach (var material in materials)
            {
                material.MainTexture.Dispose();
                material.Sampler.Dispose();
                material.Dispose();
            }
        }
    }
}
