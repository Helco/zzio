using System;
using System.Linq;
using DefaultEcs.System;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;

namespace zzre.game.systems;

[With(typeof(components.ActorPart))]
public partial class ActorRenderer : AEntitySetSystem<CommandList>
{
    private static readonly FilePath[] BaseTexturePaths =
    {
        new("resources/textures/actorsex"),
        new("resources/textures/models"),
        new("resources/textures/worlds")
    };

    private readonly ITagContainer diContainer;
    private readonly GraphicsDevice graphicsDevice;
    private readonly Camera camera;
    private readonly Texture whiteTexture;
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

        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        whiteTexture = graphicsDevice.ResourceFactory.CreateTexture(new(1, 1, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled, TextureType.Texture2D));
        whiteTexture.Name = "White";
        graphicsDevice.UpdateTexture(whiteTexture, new byte[] { 255, 255, 255, 255 }, 0, 0, 0, 1, 1, 1, 0, 0);
    }

    public override void Dispose()
    {
        base.Dispose();
        whiteTexture.Dispose();
        addSubscription.Dispose();
        removeSubscription.Dispose();
    }

    protected override void PreUpdate(CommandList cl)
    {
        base.PreUpdate(cl);
        cl.PushDebugGroup(nameof(ActorRenderer));
    }

    [WithPredicate]
    private bool IsVisible(in components.Visibility vis) => vis == components.Visibility.Visible;

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
        var skeleton = entity.TryGet<Skeleton?>().GetValueOrDefault(null);
        var syncedLocation = entity.Get<components.SyncedLocation>();
        var zzreMaterials = rwMaterials.Select(rwMaterial =>
        {
            var material = new ModelMaterial(diContainer) { IsSkinned = skeleton != null };
            if (rwMaterial.FindChildById(zzio.rwbs.SectionId.Texture, recursive: true) == null)
                (material.Texture.Texture, material.Sampler.Sampler) = (whiteTexture, graphicsDevice.PointSampler);
            else
                (material.Texture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(BaseTexturePaths, rwMaterial);
            material.Factors.Ref = ModelFactors.Default with
            {
                vertexColorFactor = 0f,
                tintFactor = 1f,
            };
            material.Tint.Ref = rwMaterial.color;
            if (skeleton != null)
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
            if (material.Texture.Texture != whiteTexture)
                material.Texture.Texture?.Dispose();
            if (material.Sampler.Sampler != graphicsDevice.PointSampler)
                material.Sampler.Sampler.Dispose();
            material.Dispose();
        }
    }
}
