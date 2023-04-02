using System;
using System.Linq;
using DefaultEcs.System;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;

namespace zzre.game.systems;

[With(typeof(components.ActorPart))]
[With(typeof(components.Visibility))]
public partial class ActorRenderer : AEntitySetSystem<CommandList>
{
    private static readonly FilePath[] BaseTexturePaths =
    {
        new("resources/textures/actorsex"),
        new("resources/textures/models"),
        new("resources/textures/worlds")
    };

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
        addSubscription = World.SubscribeEntityComponentAdded<components.ActorPart>(HandleAddedComponent);
        removeSubscription = World.SubscribeEntityComponentRemoved<ModelSkinnedMaterial[]>(HandleRemovedComponent);
    }

    public override void Dispose()
    {
        base.Dispose();
        addSubscription.Dispose();
        removeSubscription.Dispose();
    }

    protected override void PreUpdate(CommandList cl)
    {
        base.PreUpdate(cl);
        cl.PushDebugGroup(nameof(ActorRenderer));
    }

    [Update]
    private void Update(CommandList cl,
        in components.Parent parent,
        in ClumpBuffers clumpBuffers,
        in ModelSkinnedMaterial[] materials)
    {
        var actorExResource = parent.Entity.Get<DefaultEcs.Resource.ManagedResource<string, ActorExDescription>>();
        cl.PushDebugGroup(actorExResource.Info);
        foreach (var (subMesh, material) in clumpBuffers.SubMeshes.Zip(materials))
        {
            if (material.Pose.Skeleton?.Animation != null)
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
        cl.PopDebugGroup();
    }

    protected override void PostUpdate(CommandList cl)
    {
        base.PostUpdate(cl);
        cl.PopDebugGroup();
    }

    private void HandleAddedComponent(in DefaultEcs.Entity entity, in components.ActorPart value)
    {
        var rwMaterials = entity.Get<ClumpBuffers>().SubMeshes.Select(sm => sm.Material);
        var skeleton = entity.Get<Skeleton>();
        var syncedLocation = entity.Get<components.SyncedLocation>();
        var zzreMaterials = rwMaterials.Select(rwMaterial =>
        {
            var material = new ModelSkinnedMaterial(diContainer);
            (material.MainTexture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(BaseTexturePaths, rwMaterial);
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
            material.MainTexture.Texture?.Dispose();
            material.Sampler.Sampler.Dispose();
            material.Dispose();
        }
    }
}
