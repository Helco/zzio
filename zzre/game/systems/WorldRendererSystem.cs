using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzre.materials;
using zzre.rendering;

namespace zzre.game.systems;

public class WorldRendererSystem : ISystem<CommandList>
{
    private static readonly FilePath BasePath = new("resources/worlds");

    public enum CullingMode
    {
        None,
        Frozen,
        FrustumCulling
    }

    private readonly ITagContainer diContainer;
    private readonly IAssetRegistry assetRegistry;
    private readonly LocationBuffer locationBuffer;
    private readonly Camera camera;
    private readonly DefaultEcs.World ecsWorld;
    private readonly IDisposable sceneLoadedSubscription;
    private readonly List<AssetHandle<WorldMaterialAsset>> materialAssetHandles = [];
    private readonly List<ModelMaterial> materials = [];
    private readonly List<WorldMesh.MeshSection> visibleMeshSections = [];
    private readonly List<StaticMesh.SubMesh> visibleSubMeshes = [];
    private readonly Queue<WorldMesh.BaseSection> visibilityQueue = []; // declared here to reduce memory allocations
    private readonly DeviceBufferRange locationRange;
    private Frustum viewFrustum;
    private AssetHandle<WorldAsset> worldAssetHandle;
    private WorldMesh? worldMesh;

    public bool IsEnabled { get; set; } = true;
    public Location Location { get; } = new();
    public CullingMode Culling { get; set; } = CullingMode.FrustumCulling;
    public Frustum ViewFrustum => viewFrustum;
    internal IReadOnlyList<ModelMaterial> Materials => materials;
    internal IReadOnlyList<WorldMesh.MeshSection> VisibleMeshSections => visibleMeshSections;

    public WorldRendererSystem(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        assetRegistry = diContainer.GetTag<IAssetRegistry>();
        locationBuffer = diContainer.GetTag<LocationBuffer>();
        camera = diContainer.GetTag<Camera>();
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        ecsWorld.SetMaxCapacity<WorldMesh>(1);
        ecsWorld.SetMaxCapacity<WorldCollider>(1);
        sceneLoadedSubscription = ecsWorld.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);

        locationRange = locationBuffer.Add(Location);
    }

    public void Dispose()
    {
        DisposeWorld();
        locationBuffer.Remove(locationRange);
        sceneLoadedSubscription.Dispose();
    }

    private void DisposeWorld()
    {
        visibleSubMeshes.Clear();

        materials.Clear();
        foreach (var handle in materialAssetHandles)
            handle.Dispose();
        materialAssetHandles.Clear();
        worldAssetHandle.Dispose();
        worldMesh = null;
    }

    private void HandleSceneLoaded(in messages.SceneLoaded message) =>
        LoadWorld(BasePath.Combine(message.Scene.misc.worldFile + ".bsp"));

    internal void LoadWorld(FilePath path)
    {
        DisposeWorld();

        worldAssetHandle = assetRegistry.LoadWorld(path, AssetLoadPriority.Synchronous);
        worldMesh = worldAssetHandle.Get().Mesh;
        visibleMeshSections.EnsureCapacity(worldMesh.Sections.Count(s => s is WorldMesh.MeshSection));
        visibleSubMeshes.EnsureCapacity(worldMesh.SubMeshes.Count);
        visibilityQueue.EnsureCapacity(worldMesh.Sections.Count);

        materialAssetHandles.EnsureCapacity(worldMesh.Materials.Count);
        materials.EnsureCapacity(worldMesh.Materials.Count);
        foreach (var rwMaterial in worldMesh.Materials)
        {
            var handle = assetRegistry.LoadWorldMaterial(rwMaterial, AssetLoadPriority.Synchronous);
            materialAssetHandles.Add(handle);
            var material = handle.Get().Material;
            material.World.BufferRange = locationRange;
            material.Tint.Ref = FColor.White;
            materials.Add(material);
        }

        ecsWorld.Set(worldMesh);
        ecsWorld.Set(WorldCollider.Create(worldMesh.World));
    }

    public void Update(CommandList cl)
    {
        if (!IsEnabled || worldMesh is null)
            return;
        if (Culling == CullingMode.FrustumCulling)
            UpdateVisibilityByFrustumCulling();
        else if (Culling == CullingMode.None)
            UpdateVisibilityToAll();
        if (visibleSubMeshes.Count == 0)
            return;

        cl.PushDebugGroup(nameof(WorldRendererSystem));
        bool didSetBuffers = false;
        int lastMaterial = -1;
        foreach (var subMesh in visibleSubMeshes)
        {
            if (lastMaterial != subMesh.Material)
            {
                lastMaterial = subMesh.Material;
                (materials[subMesh.Material] as IMaterial).Apply(cl);
            }
            if (!didSetBuffers)
            {
                didSetBuffers = true;
                materials[subMesh.Material].ApplyAttributes(cl, worldMesh);
                cl.SetIndexBuffer(worldMesh.IndexBuffer, worldMesh.IndexFormat);
            }
            cl.DrawIndexed(
                indexStart: (uint)subMesh.IndexOffset,
                indexCount: (uint)subMesh.IndexCount,
                instanceCount: 1,
                vertexOffset: 0,
                instanceStart: 0);
        }
        cl.PopDebugGroup();
    }

    private void UpdateVisibilityByFrustumCulling()
    {
        if (worldMesh is null)
            return;
        viewFrustum.Projection = camera.View * camera.Projection;
        visibleMeshSections.Clear();
        visibleSubMeshes.Clear();
        visibilityQueue.Clear();

        visibilityQueue.Enqueue(worldMesh.Sections[0]);
        while (visibilityQueue.Any())
        {
            var section = visibilityQueue.Dequeue();
            if (section is WorldMesh.MeshSection meshSection)
            {
                visibleMeshSections.Add(meshSection);
                visibleSubMeshes.AddRange(worldMesh.GetSubMeshes(meshSection.SubMeshSection));
                continue;
            }

            var plane = (WorldMesh.PlaneSection)section;
            var intersection = viewFrustum.Intersects(new Plane(plane.PlaneType.AsNormal(), plane.RightValue));
            if (intersection.HasFlag(PlaneIntersections.Inside))
                visibilityQueue.Enqueue(plane.RightChild);

            intersection = viewFrustum.Intersects(new Plane(plane.PlaneType.AsNormal(), plane.LeftValue));
            if (intersection.HasFlag(PlaneIntersections.Outside))
                visibilityQueue.Enqueue(plane.LeftChild);
        }

        visibleSubMeshes.Sort(StaticMesh.CompareByMaterial);
    }

    private void UpdateVisibilityToAll()
    {
        if (worldMesh is null || visibleSubMeshes.Count == worldMesh.SubMeshes.Count)
            return;
        visibleSubMeshes.Clear();
        visibleSubMeshes.AddRange(worldMesh.GetSubMeshes());
        visibleSubMeshes.Sort(StaticMesh.CompareByMaterial);
        visibleMeshSections.Clear();
        visibleMeshSections.AddRange(worldMesh.Sections.OfType<WorldMesh.MeshSection>());
    }
}
