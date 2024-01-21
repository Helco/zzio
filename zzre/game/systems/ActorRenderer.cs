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
        removeSubscription = World.SubscribeEntityComponentRemoved<ModelMaterial[]>(HandleRemovedComponent);
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
        in DefaultEcs.Entity entity,
        in ClumpMesh clumpMesh,
        in ModelMaterial[] materials)
    {
        var actorExResource = entity.TryGet<components.Parent>(out var parent) && parent.Entity.IsAlive
            ? parent.Entity.Get<DefaultEcs.Resource.ManagedResource<string, ActorExDescription>>()
            : default;
        cl.PushDebugGroup(actorExResource.Info ?? "Unknown Actor");
        materials.First().ApplyAttributes(cl, clumpMesh);
        cl.SetIndexBuffer(clumpMesh.IndexBuffer, clumpMesh.IndexFormat);
        foreach (var subMesh in clumpMesh.SubMeshes)
        {
            var material = materials[subMesh.Material];
            if (material.Pose.Skeleton?.Animation != null)
                material.Pose.MarkPoseDirty();
            (material as IMaterial).Apply(cl);
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
        var rwMaterials = entity.Get<ClumpMesh>().Materials;
        var skeleton = entity.Get<Skeleton>();
        var syncedLocation = entity.Get<components.SyncedLocation>();
        var zzreMaterials = rwMaterials.Select(rwMaterial =>
        {
            var material = new ModelMaterial(diContainer) { IsSkinned = true };
            (material.Texture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(BaseTexturePaths, rwMaterial);
            material.Factors.Ref = ModelFactors.Default with
            {
                vertexColorFactor = 0f
            };
            material.Tint.Ref = rwMaterial.color;
            material.Pose.Skeleton = skeleton;
            material.LinkTransformsTo(camera);
            material.World.BufferRange = syncedLocation.BufferRange;
            return material;
        }).ToArray();
        entity.Set(zzreMaterials);
    }

    private void HandleRemovedComponent(in DefaultEcs.Entity entity, in ModelMaterial[] materials)
    {
        foreach (var material in materials)
        {
            material.Texture.Texture?.Dispose();
            material.Sampler.Sampler.Dispose();
            material.Dispose();
        }
    }
}
