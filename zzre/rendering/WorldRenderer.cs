using System;
using System.Collections.Generic;
using System.Linq;
using zzio.rwbs;
using zzre.materials;
using Veldrid;
using zzio;

namespace zzre.rendering;

public class WorldRenderer : BaseDisposable
{
    private readonly ITagContainer diContainer;
    private readonly GraphicsDevice graphicsDevice;
    private readonly DeviceBufferRange locationRange;
    private readonly Camera camera;
    private WorldMesh? worldMesh;
    public WorldMesh? WorldMesh
    {
        get => worldMesh;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            worldMesh = value;
            LoadMaterials();
            visibleMeshSections.Clear();
        }
    }
    public Texture WhiteTexture { get; }

    private Frustum viewFrustum;
    private ModelMaterial[] materials = [];
    public IReadOnlyList<ModelMaterial> Materials => materials;

    private readonly List<WorldMesh.MeshSection> visibleMeshSections = [];
    public IReadOnlyList<WorldMesh.MeshSection> VisibleMeshSections => visibleMeshSections;

    public Location Location { get; } = new Location();
    public Frustum ViewFrustum => viewFrustum;

    public WorldRenderer(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        camera = diContainer.GetTag<Camera>();
        var locationBuffer = diContainer.GetTag<LocationBuffer>();
        locationRange = locationBuffer.Add(Location);

        graphicsDevice = diContainer.GetTag<GraphicsDevice>();
        WhiteTexture = graphicsDevice.ResourceFactory.CreateTexture(new(1, 1, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled, TextureType.Texture2D));
        WhiteTexture.Name = "White";
        graphicsDevice.UpdateTexture(WhiteTexture, new byte[] { 255, 255, 255, 255 }, 0, 0, 0, 1, 1, 1, 0, 0);
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        diContainer.GetTag<LocationBuffer>().Remove(locationRange);
        DisposeMaterials();
        WhiteTexture.Dispose();
    }

    private void DisposeMaterials()
    {
        var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
        foreach (var material in materials)
        {
            if (textureLoader is not CachedAssetLoader<Texture> && material.Texture.Texture != WhiteTexture)
            {
                material.Texture.Texture?.Dispose();
                material.Sampler.Sampler?.Dispose();
            }
            material.Dispose();
        }
        materials = [];
    }

    public void LoadMaterials()
    {
        var textureBase = new FilePath("resources/textures/worlds");
        var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
        var camera = diContainer.GetTag<Camera>();

        DisposeMaterials();

        if (worldMesh == null)
            return;
        diContainer.TryGetTag(out UniformBuffer<FogParams> fogParams);
        materials = new ModelMaterial[worldMesh.Materials.Count];
        foreach (var (rwMaterial, index) in worldMesh.Materials.Indexed())
        {
            var material = materials[index] = new ModelMaterial(diContainer) { HasFog = fogParams != null };
            if (rwMaterial.isTextured)
                (material.Texture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(textureBase, rwMaterial);
            else
            {
                material.Texture.Texture = WhiteTexture;
                material.Sampler.Sampler = graphicsDevice.PointSampler;
            }
            material.LinkTransformsTo(camera);
            material.World.BufferRange = locationRange;
            material.Tint.Ref = FColor.White;
            material.Factors.Ref = ModelFactors.Default;
            if (fogParams != null)
                material.FogParams.Buffer = fogParams.Buffer;
        }
    }

    public void UpdateVisibility()
    {
        if (worldMesh == null)
            return;

        viewFrustum.Projection = camera.View * camera.Projection;
        visibleMeshSections.Clear();
        var sectionQueue = new Queue<WorldMesh.BaseSection>();
        sectionQueue.Enqueue(worldMesh.Sections.First());
        while (sectionQueue.Any())
        {
            var section = sectionQueue.Dequeue();
            if (section is WorldMesh.MeshSection meshSection)
            {
                visibleMeshSections.Add(meshSection);
                continue;
            }

            var plane = (WorldMesh.PlaneSection)section;
            var intersection = viewFrustum.Intersects(new Plane(plane.PlaneType.AsNormal(), plane.RightValue));
            if (intersection.HasFlag(PlaneIntersections.Inside))
                sectionQueue.Enqueue(plane.RightChild);
            intersection = viewFrustum.Intersects(new Plane(plane.PlaneType.AsNormal(), plane.LeftValue));
            if (intersection.HasFlag(PlaneIntersections.Outside))
                sectionQueue.Enqueue(plane.LeftChild);
        }
    }

    public void Render(CommandList cl, IEnumerable<WorldMesh.MeshSection>? sections = null)
    {
        if (worldMesh == null)
            return;
        sections ??= visibleMeshSections;

        cl.PushDebugGroup(nameof(WorldRenderer));
        var visibleSubMeshes = sections
            .SelectMany(m => worldMesh.GetSubMeshes(m.SubMeshSection))
            .GroupBy(s => s.Material);
        bool didSetBuffers = false;
        foreach (var group in visibleSubMeshes)
        {
            (materials[group.Key] as IMaterial).Apply(cl);
            if (!didSetBuffers)
            {
                didSetBuffers = true;
                materials[group.Key].ApplyAttributes(cl, worldMesh);
                cl.SetIndexBuffer(worldMesh.IndexBuffer, worldMesh.IndexFormat);
            }
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
        cl.PopDebugGroup();
    }

    public void RenderForceAll(CommandList cl) =>
        Render(cl, worldMesh?.Sections.OfType<WorldMesh.MeshSection>());
}
