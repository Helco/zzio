using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Veldrid;
using ImGuiNET;
using zzio;
using zzio.rwbs;
using zzio.vfs;
using zzre.rendering;
using zzre.materials;
using zzre.debug;
using zzre.imgui;

namespace zzre.tools;

public class ModelViewer : ListDisposable, IDocumentEditor
{
    private const byte DebugPlaneAlpha = 0xA0;

    private readonly ITagContainer diContainer;
    private readonly TwoColumnEditorTag editor;
    private readonly Camera camera;
    private readonly OrbitControlsTag controls;
    private readonly ImGuiRenderer imGuiRenderer;
    private readonly GraphicsDevice device;
    private readonly FramebufferArea fbArea;
    private readonly IAssetLoader<Texture> textureLoader;
    private readonly IResourcePool resourcePool;
    private readonly DebugLineRenderer gridRenderer;
    private readonly DebugLineRenderer triangleRenderer;
    private readonly DebugLineRenderer normalRenderer;
    private readonly DebugPlaneRenderer planeRenderer;
    private readonly OpenFileModal openFileModal;
    private readonly ModelMaterialEdit modelMaterialEdit;
    private readonly LocationBuffer locationBuffer;

    private ClumpMesh? mesh;
    private GeometryTreeCollider? collider;
    private ModelMaterial[] materials = Array.Empty<ModelMaterial>();
    private DebugSkeletonRenderer? skeletonRenderer;
    private int highlightedSplitI = -1;
    private bool showNormals;

    public Window Window { get; }
    public IResource? CurrentResource { get; private set; }

    public ModelViewer(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        device = diContainer.GetTag<GraphicsDevice>();
        resourcePool = diContainer.GetTag<IResourcePool>();
        textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
        Window = diContainer.GetTag<WindowContainer>().NewWindow("Model Viewer");
        Window.InitialBounds = new Rect(float.NaN, float.NaN, 1100.0f, 600.0f);
        Window.AddTag(this);
        editor = new TwoColumnEditorTag(Window, diContainer);
        var onceAction = new OnceAction();
        Window.AddTag(onceAction);
        Window.OnContent += onceAction.Invoke;
        var menuBar = new MenuBarWindowTag(Window);
        menuBar.AddButton("Open", HandleMenuOpen);
        menuBar.AddCheckbox("View/Normals", () => ref showNormals, () => fbArea!.IsDirty = true);
        fbArea = Window.GetTag<FramebufferArea>();
        fbArea.OnResize += HandleResize;
        fbArea.OnRender += HandleRender;
        modelMaterialEdit = new ModelMaterialEdit(Window, diContainer);
        diContainer.GetTag<OpenDocumentSet>().AddEditor(this);

        openFileModal = new OpenFileModal(diContainer)
        {
            Filter = "*.dff",
            IsFilterChangeable = false
        };
        openFileModal.OnOpenedResource += Load;
        imGuiRenderer = Window.Container.ImGuiRenderer;

        locationBuffer = new LocationBuffer(device);
        AddDisposable(locationBuffer);

        var localDiContainer = diContainer.ExtendedWith(locationBuffer);
        camera = new Camera(localDiContainer);
        AddDisposable(camera);

        controls = new OrbitControlsTag(Window, camera.Location, localDiContainer);
        AddDisposable(controls);

        gridRenderer = new DebugLineRenderer(diContainer);
        gridRenderer.Material.LinkTransformsTo(camera);
        gridRenderer.Material.World.Ref = Matrix4x4.Identity;
        gridRenderer.AddGrid();
        AddDisposable(gridRenderer);

        triangleRenderer = new DebugLineRenderer(diContainer);
        triangleRenderer.Material.LinkTransformsTo(camera);
        triangleRenderer.Material.World.Ref = Matrix4x4.Identity;
        AddDisposable(triangleRenderer);

        normalRenderer = new DebugLineRenderer(diContainer);
        normalRenderer.Material.LinkTransformsTo(camera);
        normalRenderer.Material.World.Ref = Matrix4x4.Identity;
        AddDisposable(normalRenderer);

        planeRenderer = new DebugPlaneRenderer(diContainer);
        planeRenderer.Material.LinkTransformsTo(camera);
        planeRenderer.Material.World.Ref = Matrix4x4.Identity;
        AddDisposable(planeRenderer);

        editor.AddInfoSection("Statistics", HandleStatisticsContent);
        editor.AddInfoSection("Materials", HandleMaterialsContent);
        editor.AddInfoSection("Skeleton", HandleSkeletonContent);
        editor.AddInfoSection("Collision", HandleCollisionContent);
    }

    public void Load(string pathText)
    {
        var resource = resourcePool.FindFile(pathText);
        if (resource == null)
            throw new FileNotFoundException($"Could not find model at {pathText}");
        Load(resource);
    }

    public void Load(IResource resource) =>
        Window.GetTag<OnceAction>().Next += () => LoadModelNow(resource);

    private void LoadModelNow(IResource resource)
    {
        if (resource.Equals(CurrentResource))
            return;
        CurrentResource = null;
        var texturePaths = new[]
        {
            textureLoader.GetTexturePathFromModel(resource.Path),
            new FilePath("resources/textures/models"),
            new FilePath("resources/textures/worlds"),
        };

        normalRenderer.Clear();
        // TODO: ModelViewer should dispose meshes and materials after loading a different model

        mesh = new ClumpMesh(diContainer, resource);
        AddDisposable(mesh);

        materials = new ModelMaterial[mesh.Materials.Count];
        foreach (var (rwMaterial, index) in mesh.Materials.Indexed())
        {
            var material = materials[index] = new ModelMaterial(diContainer);
            (material.Texture.Texture, material.Sampler.Sampler) = TryLoadTexture(texturePaths, rwMaterial);
            material.LinkTransformsTo(camera);
            material.World.Ref = Matrix4x4.Identity;
            material.Factors.Ref = ModelFactors.Default with
            {
                vertexColorFactor = 0f, // they seem to be set to some gray for models?
            };
            material.Tint.Ref = rwMaterial.color;
            AddDisposable(material);
        }
        modelMaterialEdit.Materials = materials;

        skeletonRenderer = null;
        if (mesh.Skin != null)
        {
            var skeleton = new Skeleton(mesh.Skin, resource.Name.Replace(".DFF", "", StringComparison.CurrentCultureIgnoreCase));
            skeletonRenderer = new DebugSkeletonRenderer(diContainer.ExtendedWith(camera, locationBuffer), mesh, skeleton);
            AddDisposable(skeletonRenderer);
        }

        collider = mesh.Geometry.FindChildById(SectionId.CollisionPLG, true) == null
            ? null
            : new GeometryTreeCollider(mesh.Geometry, location: null);

        controls.ResetView();
        HighlightSplit(-1);
        fbArea.IsDirty = true;
        CurrentResource = resource;
        Window.Title = $"Model Viewer - {resource.Path.ToPOSIXString()}";
    }

    private (Texture, Sampler) TryLoadTexture(FilePath[] texturePaths, RWMaterial rwMaterial)
    {
        try
        {
            return textureLoader.LoadTexture(texturePaths, rwMaterial);
        }
        catch(Exception e) when (e is InvalidOperationException or InvalidDataException)
        {
            var texture = device.ResourceFactory.CreateTexture(new(2, 2, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm, TextureUsage.Sampled, TextureType.Texture2D));
            device.UpdateTexture(texture, new byte[]
            {
                0xff, 0x00, 0xff, 0xff,
                0xff, 0xff, 0xff, 0xff,
                0x00, 0x00, 0x00, 0xff,
                0xff, 0x00, 0xff, 0xff
            }, 0, 0, 0, 2, 2, 1, 0, 0);
            texture.Name =
                ((rwMaterial.FindChildById(SectionId.String, true) as RWString)?.value
                ?? "<unknown>") + " (Missing)";
            return (texture, device.PointSampler);
        }
    }

    private void HandleResize() => camera.Aspect = fbArea.Ratio;

    private void HandleRender(CommandList cl)
    {
        locationBuffer.Update(cl);
        camera.Update(cl);
        gridRenderer.Render(cl);
        if (mesh == null)
            return;

        foreach (var subMesh in mesh.SubMeshes)
        {
            (materials[subMesh.Material] as IMaterial).Apply(cl);
            materials[subMesh.Material].ApplyAttributes(cl, mesh, requireAll: true);
            cl.SetIndexBuffer(mesh.IndexBuffer, mesh.IndexFormat);
            cl.DrawIndexed(
                indexStart: (uint)subMesh.IndexOffset,
                indexCount: (uint)subMesh.IndexCount,
                instanceCount: 1,
                vertexOffset: 0,
                instanceStart: 0);
        }

        skeletonRenderer?.Render(cl);
        planeRenderer.Render(cl);
        triangleRenderer.Render(cl);
        if (showNormals)
        {
            if (normalRenderer.Count == 0)
                GenerateNormals();
            normalRenderer.Render(cl);
        }
    }

    private void GenerateNormals()
    {
        if (mesh == null)
            return;
        var morphTarget = mesh.Geometry.morphTargets.First();
        normalRenderer.Clear();
        normalRenderer.Add(IColor.Red, morphTarget.vertices.Zip(morphTarget.normals,
            (pos, normal) => new Line(pos, pos + Vector3.Normalize(normal) * 0.05f)));
    }

    private void HandleStatisticsContent()
    {
        ImGui.Text($"BSphere Radius: {mesh?.BoundingSphere.Radius}");
        ImGui.Text($"Vertices: {mesh?.VertexCount}");
        ImGui.Text($"Triangles: {mesh?.TriangleCount}");
        ImGui.Text($"Submeshes: {mesh?.SubMeshes.Count}");
        ImGui.Text($"Bones: {skeletonRenderer?.Skeleton.Bones.Count.ToString() ?? "none"}");
        ImGui.Text($"Collision splits: {collider?.Collision.splits.Length.ToString() ?? "none"}");
        ImGui.Text("Collision test: " + (mesh?.HasCollisionTest is true ? "yes" : "no"));
    }

    private void HandleMaterialsContent()
    {
        if (mesh == null)
            return;
        else if (modelMaterialEdit.Content())
            fbArea.IsDirty = true;
    }

    private void HandleMenuOpen()
    {
        openFileModal.InitialSelectedResource = CurrentResource;
        openFileModal.Modal.Open();
    }

    private void HandleSkeletonContent()
    {
        if (skeletonRenderer == null)
            ImGui.Text("This model has no skeleton.");
        else if (skeletonRenderer.Content())
            fbArea.IsDirty = true;
    }

    private void HighlightSplit(int splitI)
    {
        highlightedSplitI = splitI;
        triangleRenderer.Clear();
        planeRenderer.Planes = Array.Empty<DebugPlane>();
        if (collider == null || mesh == null || highlightedSplitI < 0)
            return;

        var split = collider.Collision.splits[splitI];
        var normal = split.left.type switch
        {
            zzio.rwbs.CollisionSectorType.X => Vector3.UnitX,
            zzio.rwbs.CollisionSectorType.Y => Vector3.UnitY,
            zzio.rwbs.CollisionSectorType.Z => Vector3.UnitZ,
            _ => throw new NotSupportedException($"Unsupported collision sector type: {split.left.type}")
        };
        SetPlanes(mesh.BoundingBox, normal, split.left.value, split.right.value, centerValue: null);

        triangleRenderer.AddTriangles(IColor.Red, SectorTriangles(split.left).ToArray());
        triangleRenderer.AddTriangles(IColor.Blue, SectorTriangles(split.right).ToArray());

        fbArea.IsDirty = true;

        IEnumerable<Triangle> SplitTriangles(CollisionSplit split) =>
            SectorTriangles(split.left).Concat(SectorTriangles(split.right));

        IEnumerable<Triangle> SectorTriangles(CollisionSector sector) => sector.count == RWCollision.SplitCount
            ? SplitTriangles(collider!.Collision.splits[sector.index])
            : collider!.Collision.map
                .Skip(sector.index)
                .Take(sector.count)
                .Select(i => collider.GetTriangle(i).Item1)
                .ToArray();
    }

    private void SetPlanes(Box bounds, Vector3 normal, float leftValue, float rightValue, float? centerValue)
    {
        var planarCenter = bounds.Center * (Vector3.One - normal);
        var otherSizes = bounds.Size * (Vector3.One - normal);
        var size = Math.Max(Math.Max(otherSizes.X, otherSizes.Y), otherSizes.Z) * 0.5f;
        var planes = new[]
        {
                new DebugPlane()
                {
                    center = planarCenter + normal * leftValue,
                    normal = normal,
                    size = size * 0.7f,
                    color = IColor.Red.WithA(DebugPlaneAlpha)
                },
                new DebugPlane()
                {
                    center = planarCenter + normal * rightValue,
                    normal = normal,
                    size = size * 0.7f,
                    color = IColor.Blue.WithA(DebugPlaneAlpha)
                }
        };
        if (centerValue.HasValue)
        {
            planes = planes.Append(
                new DebugPlane()
                {
                    center = planarCenter + normal * centerValue.Value,
                    normal = normal,
                    size = size,
                    color = IColor.Green.WithA(DebugPlaneAlpha)
                }).ToArray();
        }
        planeRenderer.Planes = planes;
    }

    private void HandleCollisionContent()
    {
        if (mesh == null)
        {
            ImGui.Text("No model loaded");
            return;
        }
        else if (collider == null)
        {
            ImGui.Text("No collision in model");
            return;
        }

        Split(0);
        void Split(int splitI)
        {
            var split = collider.Collision.splits[splitI];
            var flags = (splitI == highlightedSplitI ? ImGuiTreeNodeFlags.Selected : 0) |
                ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.OpenOnArrow |
                ImGuiTreeNodeFlags.DefaultOpen;
            var isOpen = ImGui.TreeNodeEx($"{split.left.type} {split.left.value}-{split.right.value}", flags);
            if (ImGui.IsItemClicked() && splitI != highlightedSplitI)
                HighlightSplit(splitI);

            if (isOpen)
            {
                Sector(split.left, false);
                Sector(split.right, true);
                ImGui.TreePop();
            }
        }

        void Sector(zzio.rwbs.CollisionSector sector, bool isRight)
        {
            if (sector.count == zzio.rwbs.RWCollision.SplitCount)
                Split(sector.index);
            else
                ImGui.Text($"{(isRight ? "Right" : "Left")}: {sector.count} Triangles");
        }
    }
}
