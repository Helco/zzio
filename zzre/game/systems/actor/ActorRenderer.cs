using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using Veldrid;
using zzio;
using zzre.materials;
using zzre.rendering;

namespace zzre.game.systems;

[With(typeof(components.ActorPart))]
public partial class ActorRenderer : AEntitySetSystem<CommandList>
{
    private readonly ITagContainer diContainer;
    private readonly IAssetRegistry assetRegistry;
    private readonly GraphicsDevice graphicsDevice;
    private readonly Camera camera;
    private readonly Texture whiteTexture;
    private readonly IAssetLoader<Texture> textureLoader;
    private readonly UniformBuffer<ModelFactors> modelFactors;
    private readonly UniformBuffer<FogParams> fogParams; // this is not owned by us!
    private readonly IDisposable sceneLoadedSubscription;
    private readonly IDisposable loadActorSubscription;

    public ActorRenderer(ITagContainer diContainer) :
        base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        this.diContainer = diContainer;
        assetRegistry = diContainer.GetTag<IAssetRegistry>();
        camera = diContainer.GetTag<Camera>();
        textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
        fogParams = diContainer.GetTag<UniformBuffer<FogParams>>();
        sceneLoadedSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        loadActorSubscription = World.Subscribe<messages.LoadActor>(HandleLoadActor);

        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        whiteTexture = graphicsDevice.ResourceFactory.CreateTexture(new(1, 1, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled, TextureType.Texture2D));
        whiteTexture.Name = "White";
        graphicsDevice.UpdateTexture(whiteTexture, new byte[] { 255, 255, 255, 255 }, 0, 0, 0, 1, 1, 1, 0, 0);

        modelFactors = new(graphicsDevice.ResourceFactory);
        modelFactors.Ref = ModelFactors.Default with
        {
            vertexColorFactor = 0f,
            tintFactor = 1f,
            ambient = Vector4.One
        };
    }

    public override void Dispose()
    {
        base.Dispose();
        modelFactors.Dispose();
        whiteTexture.Dispose();
        sceneLoadedSubscription.Dispose();
        loadActorSubscription.Dispose();
    }

    protected override void PreUpdate(CommandList cl)
    {
        base.PreUpdate(cl);
        cl.PushDebugGroup(nameof(ActorRenderer));
        modelFactors.Update(cl);
    }

    [WithPredicate]
    private bool IsVisible(in components.Visibility vis) => vis == components.Visibility.Visible;

    [Update]
    private static void Update(CommandList cl,
        in DefaultEcs.Entity entity,
        in ClumpMesh clumpMesh,
        in ModelMaterial[] materials)
    {
        var actorName =
            entity.TryGet(out components.Parent parent) &&
            parent.Entity.TryGet(out AssetHandle handle)
            ? handle.Get<ActorAsset>().Name
            : "Unknown actor";

        cl.PushDebugGroup(actorName);
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

    private void HandleSceneLoaded(in messages.SceneLoaded msg)
    {
        modelFactors.Ref.ambient = msg.Scene.misc.ambientLight.ToNumerics();
    }

    private unsafe void HandleLoadActor(in messages.LoadActor msg)
    {
        var handle = assetRegistry.Load(
            new ActorAsset.Info(msg.ActorName),
            msg.Priority,
            &ApplyActorToEntity,
            (this, msg.AsEntity));
        msg.AsEntity.Set(handle);
    }

    private static void ApplyActorToEntity(AssetHandle handle,
        ref readonly (ActorRenderer thiz, DefaultEcs.Entity entity) context)
    {
        var (thiz, entity) = context;
        if (!entity.IsAlive)
            return;
        var asset = handle.Get<ActorAsset>();
        entity.Set(asset.Description);
        var body = thiz.CreateActorPart(entity, asset.Body, asset.BodyAnimations, asset.Description.body);
        var wings = asset.Description.HasWings
            ? thiz.CreateActorPart(entity, asset.Wings, asset.WingsAnimations, asset.Description.wings)
            : null as DefaultEcs.Entity?;
        entity.Set(new components.ActorParts(body, wings));

        // attach to the "grandparent" as only animals are controlled directly by the entity
        // (not meant as an insult by the way)
        body.Get<Location>().Parent = entity.Get<Location>().Parent;
        var bodySkeleton = body.Get<Skeleton>();
        bodySkeleton.Location.Parent = body.Get<Location>();

        if (wings is not null)
        {
            var wingsParentBone = bodySkeleton.Bones[asset.Description.attachWingsToBone];
            wings.Value.Get<Location>().Parent = wingsParentBone;
        }
    }

    private DefaultEcs.Entity CreateActorPart(
        DefaultEcs.Entity parent,
        AssetHandle<ClumpAsset> clumpHandle,
        IReadOnlyList<AssetHandle<AnimationAsset>> animations,
        ActorPartDescription partDescr)
    {
        var clumpAsset = clumpHandle.Get();
        var part = parent.World.CreateEntity();
        part.Set<components.SyncedLocation>();
        part.Set<components.Visibility>();
        part.Set<components.ActorPart>();
        part.Set(new components.AnimationPool()); // do not use default to keep the parametereless constructor call
        part.Set(new components.Parent(parent));
        part.Set(clumpHandle);
        part.Set(clumpAsset.Mesh);
        if (clumpAsset.Mesh.Skin is not null) // unfortunately there are some unskinned actor parts
            part.Set(new Skeleton(clumpAsset.Mesh.Skin, clumpAsset.Name));

        ref var animationPool = ref part.Get<components.AnimationPool>();
        for (int i = 0; i < animations.Count; i++)
            animationPool.Add(partDescr.animations[i].type, animations[i].Get().Animation);

        LoadActorPartMaterials(part, clumpAsset.Mesh);

        return part;
    }

    private void LoadActorPartMaterials(DefaultEcs.Entity entity, ClumpMesh mesh)
    {
        var materials = new ModelMaterial[mesh.Materials.Count];
        var handles = new AssetHandle[materials.Length];
        for (int i = 0; i < handles.Length; i++)
        {
            entity.TryGet(out Skeleton skeleton);
            var materialHandle = assetRegistry.LoadActorMaterial(mesh.Materials[i], isSkinned: skeleton is not null);
            handles[i] = materialHandle;
            var material = materials[i] = materialHandle.Get().Material;
            material.World.BufferRange = entity.Get<components.SyncedLocation>().BufferRange;
            material.Factors.Buffer = modelFactors.Buffer;
            if (skeleton is not null)
                material.Pose.Skeleton = skeleton;
        }
        entity.Set(materials);
        entity.Set(handles);
    }
}
