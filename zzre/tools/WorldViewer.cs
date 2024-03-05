using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzio.vfs;
using zzre.debug;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using zzre.game.systems;
using KeyCode = Silk.NET.SDL.KeyCode;
using static ImGuiNET.ImGui;

namespace zzre.tools;

public class WorldViewer : ListDisposable, IDocumentEditor
{
    private enum IntersectionPrimitive
    {
        None,
        Ray,
        Box,
        OrientedBox,
        Sphere,
        Triangle,
        Line
    }

    private const byte DebugPlaneAlpha = 0xA0;

    private readonly ITagContainer localDiContainer;
    private readonly DefaultEcs.World ecsWorld;
    private readonly AssetLocalRegistry assetRegistry;
    private readonly TwoColumnEditorTag editor;
    private readonly FlyControlsTag controls;
    private readonly FramebufferArea fbArea;
    private readonly IResourcePool resourcePool;
    private readonly OpenFileModal openFileModal;
    private readonly ModelMaterialEdit modelMaterialEdit;
    private readonly DebugLineRenderer boundsRenderer;
    private readonly DebugLineRenderer rayRenderer;
    private readonly DebugPlaneRenderer planeRenderer;
    private readonly DebugLineRenderer frustumRenderer;
    private readonly DebugLineRenderer triangleRenderer;
    private readonly WorldRendererSystem worldRenderer;
    private readonly Camera camera;
    private readonly LocationBuffer locationBuffer;

    private readonly UniformBuffer<Matrix4x4> worldTransform;
    private WorldMesh? worldMesh;
    private WorldCollider? worldCollider;
    private RWAtomicSection? sectionAtomic;
    private RWCollision? sectionCollision;
    private int[] sectionDepths = [];
    private int highlightedSectionI = -1;
    private int highlightedSplitI = -1;
    private IntersectionPrimitive intersectionPrimitive;
    private bool updateIntersectionPrimitive;
    private float intersectionSize = 0.5f;
    private bool showVertexColors;
    private bool setRaycastToCamera;
    private Vector3 raycastStart;
    private Vector3 raycastDir;

    public IResource? CurrentResource { get; private set; }
    public Window Window { get; }

    public WorldViewer(ITagContainer diContainer)
    {
        resourcePool = diContainer.GetTag<IResourcePool>();
        Window = diContainer.GetTag<WindowContainer>().NewWindow("World Viewer");
        Window.AddTag(this);
        Window.InitialBounds = new Rect(float.NaN, float.NaN, 1100.0f, 600.0f);
        editor = new TwoColumnEditorTag(Window, diContainer);
        var onceAction = new OnceAction();
        Window.AddTag(onceAction);
        Window.OnContent += onceAction.Invoke;
        Window.OnContent += UpdateIntersectionPrimitive;
        Window.OnKeyDown += HandleKeyDown;
        var menuBar = new MenuBarWindowTag(Window);
        menuBar.AddButton("Open", HandleMenuOpen);
        menuBar.AddCheckbox("View/Vertex Colors", () => ref showVertexColors, HandleShowVertexColors);
        var gridRenderer = new DebugLineRenderer(diContainer);
        //gridRenderer.Material.LinkTransformsTo(controls.Projection, controls.View, controls.World);
        gridRenderer.AddGrid();
        AddDisposable(gridRenderer);
        modelMaterialEdit = new ModelMaterialEdit(Window, diContainer)
        {
            OpenEntriesByDefault = false
        };
        diContainer.GetTag<OpenDocumentSet>().AddEditor(this);
        openFileModal = new OpenFileModal(diContainer)
        {
            Filter = "*.bsp",
            IsFilterChangeable = false
        };
        openFileModal.OnOpenedResource += Load;

        locationBuffer = new LocationBuffer(diContainer.GetTag<GraphicsDevice>());
        AddDisposable(locationBuffer);
        camera = new Camera(diContainer.ExtendedWith(locationBuffer));
        AddDisposable(camera);
        controls = new FlyControlsTag(Window, camera.Location, diContainer);
        worldTransform = new UniformBuffer<Matrix4x4>(diContainer.GetTag<GraphicsDevice>().ResourceFactory)
        {
            Ref = Matrix4x4.Identity
        };
        AddDisposable(worldTransform);
        fbArea = Window.GetTag<FramebufferArea>();
        fbArea.OnResize += HandleResize;
        fbArea.OnRender += locationBuffer.Update;
        fbArea.OnRender += worldTransform.Update;
        fbArea.OnRender += camera.Update;
        fbArea.OnRender += gridRenderer.Render;
        fbArea.OnRender += HandleRender;

        editor.AddInfoSection("Statistics", HandleStatisticsContent);
        editor.AddInfoSection("Materials", HandleMaterialsContent, false);
        editor.AddInfoSection("Sections", HandleSectionsContent, false);
        editor.AddInfoSection("ViewFrustum Culling", HandleViewFrustumCulling, false);
        editor.AddInfoSection("Collision", HandleCollision, false);
        editor.AddInfoSection("Raycast", HandleRaycast, true);

        boundsRenderer = new DebugLineRenderer(diContainer);
        boundsRenderer.Material.LinkTransformsTo(camera);
        boundsRenderer.Material.LinkTransformsTo(world: worldTransform);
        AddDisposable(boundsRenderer);

        planeRenderer = new DebugPlaneRenderer(diContainer);
        planeRenderer.Material.LinkTransformsTo(camera);
        planeRenderer.Material.LinkTransformsTo(world: worldTransform);
        AddDisposable(planeRenderer);

        frustumRenderer = new DebugLineRenderer(diContainer);
        frustumRenderer.Material.LinkTransformsTo(camera);
        frustumRenderer.Material.LinkTransformsTo(world: worldTransform);
        AddDisposable(frustumRenderer);

        triangleRenderer = new DebugLineRenderer(diContainer);
        triangleRenderer.Material.LinkTransformsTo(camera);
        triangleRenderer.Material.LinkTransformsTo(world: worldTransform);
        AddDisposable(triangleRenderer);

        rayRenderer = new DebugLineRenderer(diContainer);
        rayRenderer.Material.LinkTransformsTo(camera);
        rayRenderer.Material.LinkTransformsTo(world: worldTransform);
        AddDisposable(rayRenderer);

        localDiContainer = diContainer.ExtendedWith(camera, locationBuffer);
        AddDisposable(localDiContainer);
        localDiContainer
            .AddTag(ecsWorld = new DefaultEcs.World())
            .AddTag<IAssetRegistry>(assetRegistry = new AssetLocalRegistry("WorldViewer", localDiContainer));
        AssetRegistry.SubscribeAt(ecsWorld);
        assetRegistry.DelayDisposals = false;
        worldRenderer = new(localDiContainer);
        AddDisposable(worldRenderer);
    }

    protected override void DisposeManaged()
    {
        localDiContainer.RemoveTag<IAssetRegistry>(dispose: false);
        base.DisposeManaged();
        assetRegistry.Dispose();
    }

    public void Load(string pathText)
    {
        var resource = resourcePool.FindFile(pathText) ?? throw new FileNotFoundException($"Could not find world at {pathText}");
        Load(resource);
    }

    public void Load(IResource resource) =>
        Window.GetTag<OnceAction>().Next += () => LoadWorldNow(resource);

    private void LoadWorldNow(IResource resource)
    {
        if (resource.Equals(CurrentResource))
            return;
        CurrentResource = null;
        showVertexColors = false;

        worldRenderer.LoadWorld(resource.Path);
        worldMesh = ecsWorld.Get<WorldMesh>();
        worldCollider = ecsWorld.Get<WorldCollider>();
        modelMaterialEdit.Materials = worldRenderer.Materials;

        CurrentResource = resource;
        UpdateSectionDepths();
        HighlightSection(-1);
        controls.ResetView();
        camera.Location.LocalPosition = -worldMesh.Origin;
        fbArea.IsDirty = true;
        Window.Title = $"World Viewer - {resource.Path.ToPOSIXString()}";
    }

    private void UpdateSectionDepths()
    {
        // the section depth makes the ImGui tree content much easier
        if (worldMesh == null)
            throw new InvalidOperationException();
        sectionDepths = new int[worldMesh.Sections.Count];
        for (int i = 0; i < worldMesh.Sections.Count; i++)
        {
            int depth = 0;
            var curSection = worldMesh.Sections[i];
            while (curSection.Parent != null)
            {
                depth++;
                curSection = curSection.Parent;
            }
            sectionDepths[i] = depth;
        }
    }

    private void HandleRender(CommandList cl)
    {
        if (worldMesh == null)
            return;

        worldRenderer.Update(cl);

        if (highlightedSectionI >= 0)
        {
            boundsRenderer.Render(cl);
            planeRenderer.Render(cl);
        }

        if (worldRenderer.Culling == WorldRendererSystem.CullingMode.Frozen)
            frustumRenderer.Render(cl);

        triangleRenderer.Render(cl);
        rayRenderer.Render(cl);
    }

    private void HandleResize() => camera.Aspect = fbArea.Ratio;

    private void HandleKeyDown(KeyCode key)
    {
        if (key == KeyCode.KSpace)
            ShootRay();
    }

    private void HandleMenuOpen()
    {
        openFileModal.InitialSelectedResource = CurrentResource;
        openFileModal.Modal.Open();
    }

    private void HandleShowVertexColors()
    {
        if (showVertexColors)
        {
            var stdTextures = localDiContainer.GetTag<StandardTextures>();
            foreach (var material in worldRenderer.Materials)
                material.Texture.Texture = stdTextures.White;
        }
        else
        {

        }
        fbArea.IsDirty = true;
    }

    private void HandleStatisticsContent()
    {
        Text($"Vertices: {worldMesh?.VertexCount ?? 0}");
        Text($"Triangles: {worldMesh?.TriangleCount ?? 0}");
        Text($"Planes: {worldMesh?.Sections.Count(s => s is WorldMesh.PlaneSection) ?? 0}");
        Text($"Atomics: {worldMesh?.Sections.Count(s => s is WorldMesh.MeshSection) ?? 0}");
        Text($"SubMeshes: {worldMesh?.SubMeshes.Count ?? 0}");
        Text($"Materials: {worldMesh?.Materials.Count ?? 0}");
    }

    private void HandleMaterialsContent()
    {
        if (worldMesh == null)
            return;
        else if (modelMaterialEdit.Content())
            fbArea.IsDirty = true;
    }

    private void HandleSectionsContent()
    {
        if (worldMesh == null)
            return;

        if (Button("Clear selection"))
        {
            HighlightSection(-1);
            fbArea.IsDirty = true;
        }

        int curDepth = 0;
        for (int i = 0; i < worldMesh.Sections.Count; i++)
        {
            if (curDepth < sectionDepths[i])
                continue;
            while (curDepth > sectionDepths[i])
            {
                TreePop();
                curDepth--;
            }

            var section = worldMesh.Sections[i];
            if (section is WorldMesh.MeshSection meshSection)
                MeshSectionContent(meshSection, i);
            else if (section is WorldMesh.PlaneSection planeSection)
                PlaneSectionContent(planeSection, i);
        }
        while (curDepth > 0)
        {
            TreePop();
            curDepth--;
        }

        bool SectionHeaderContent(string title, int index)
        {
            var flags = (index == highlightedSectionI ? ImGuiTreeNodeFlags.Selected : 0) |
                    ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.OpenOnArrow |
                    ImGuiTreeNodeFlags.DefaultOpen;
            var isOpen = TreeNodeEx(title, flags);
            if (isOpen)
                curDepth++;
            if (IsItemClicked() && index != highlightedSectionI)
                HighlightSection(index);
            return isOpen;
        }

        void MeshSectionContent(WorldMesh.MeshSection section, int index)
        {
            bool isVisible = worldRenderer.VisibleMeshSections.Contains(section);
            Text(isVisible ? IconFonts.ForkAwesome.Eye : IconFonts.ForkAwesome.EyeSlash);

            SameLine();
            if (!SectionHeaderContent($"MeshSection #{index}", index))
                return;
            Text($"Vertices: {section.VertexCount}");
            Text($"Triangles: {section.TriangleCount}");
            Text($"SubMeshes: {worldMesh.GetSubMeshesLEGACY(section.SubMeshSection).Count()}");
        }

        void PlaneSectionContent(WorldMesh.PlaneSection section, int index)
        {
            var icon = section.PlaneType == RWPlaneSectionType.XPlane
                ? IconFonts.ForkAwesome.ArrowsH
                : section.PlaneType == RWPlaneSectionType.YPlane
                ? IconFonts.ForkAwesome.ArrowsV
                : IconFonts.ForkAwesome.Expand;
            if (!SectionHeaderContent($"{icon} {section.PlaneType} at {section.CenterValue}", index))
                return;
        }
    }

    private void HighlightSection(int index)
    {
        highlightedSectionI = index;
        HighlightSplit(-1);
        sectionAtomic = null;
        sectionCollision = null;
        if (worldMesh == null || highlightedSectionI < 0)
            return;
        boundsRenderer.Clear();
        boundsRenderer.AddBox(worldMesh.Sections[index].Bounds, IColor.Red);
        planeRenderer.Planes = [];

        if (worldMesh.Sections[index] is WorldMesh.PlaneSection planeSection)
        {
            SetPlanes(planeSection.Bounds, planeSection.PlaneType.AsNormal(), planeSection.LeftValue, planeSection.RightValue, planeSection.CenterValue);
        }
        else if (worldMesh.Sections[index] is WorldMesh.MeshSection meshSection)
        {
            sectionAtomic = meshSection.AtomicSection;
            sectionCollision = (RWCollision?)sectionAtomic.FindChildById(SectionId.CollisionPLG, recursive: true);
        }

        fbArea.IsDirty = true;
    }

    private void HighlightSplit(int splitI)
    {
        highlightedSplitI = splitI;
        triangleRenderer.Clear();
        if (worldMesh == null || sectionCollision == null || sectionAtomic == null || splitI < 0)
            return;

        var split = sectionCollision.splits[splitI];
        var normal = split.left.type switch
        {
            CollisionSectorType.X => Vector3.UnitX,
            CollisionSectorType.Y => Vector3.UnitY,
            CollisionSectorType.Z => Vector3.UnitZ,
            _ => throw new NotSupportedException($"Unsupported collision sector type: {split.left.type}")
        };
        SetPlanes(worldMesh.Sections[highlightedSectionI].Bounds, normal, split.left.value, split.right.value, centerValue: null);

        triangleRenderer.AddTriangles(IColor.Red, SectorTriangles(split.left).ToArray());
        triangleRenderer.AddTriangles(IColor.Blue, SectorTriangles(split.right).ToArray());

        fbArea.IsDirty = true;

        IEnumerable<Triangle> SplitTriangles(CollisionSplit split) =>
            SectorTriangles(split.left).Concat(SectorTriangles(split.right));

        IEnumerable<Triangle> SectorTriangles(CollisionSector sector) => sector.count == RWCollision.SplitCount
            ? SplitTriangles(sectionCollision!.splits[sector.index])
            : sectionCollision!.map
                .Skip(sector.index)
                .Take(sector.count)
                .Select(i => sectionAtomic!.triangles[i])
                .Select(t => new Triangle(
                    sectionAtomic!.vertices[t.v1],
                    sectionAtomic.vertices[t.v2],
                    sectionAtomic.vertices[t.v3]))
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
            planes =
            [
                .. planes,
                new DebugPlane()
                {
                    center = planarCenter + normal * centerValue.Value,
                    normal = normal,
                    size = size,
                    color = IColor.Green.WithA(DebugPlaneAlpha)
                },
            ];
        }
        planeRenderer.Planes = planes;
    }

    private void HandleViewFrustumCulling()
    {
        Text($"Visible meshes: {worldRenderer.VisibleMeshSections.Count}/{worldMesh?.Sections.OfType<WorldMesh.MeshSection>().Count() ?? 0}");
        var visibleTriangleCount = worldRenderer.VisibleMeshSections.Sum(s => s.TriangleCount);
        Text($"Visible triangles: {visibleTriangleCount}/{worldMesh?.TriangleCount ?? 0}");
        NewLine();

        var culling = worldRenderer.Culling;
        bool didChange = ImGuiEx.EnumRadioButtonGroup(ref culling);
        worldRenderer.Culling = culling;

        if (didChange)
        {
            frustumRenderer.Clear();
            frustumRenderer.AddHexahedron(worldRenderer.ViewFrustum.Corners, IColor.White);
            fbArea.IsDirty = true;
        }
    }

    private void HandleCollision()
    {
        if (worldMesh == null)
        {
            Text("No world loaded");
            return;
        }
        else if (highlightedSectionI < 0)
        {
            Text("No section selected");
            return;
        }
        else if (sectionCollision == null)
        {
            Text("No collision in selected section");
            return;
        }

        Split(0);
        void Split(int splitI)
        {
            var split = sectionCollision.splits[splitI];
            var flags = (splitI == highlightedSplitI ? ImGuiTreeNodeFlags.Selected : 0) |
                ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.OpenOnArrow |
                ImGuiTreeNodeFlags.DefaultOpen;
            var isOpen = TreeNodeEx($"{split.left.type} {split.left.value}-{split.right.value}", flags);
            if (IsItemClicked() && splitI != highlightedSplitI)
                HighlightSplit(splitI);

            if (isOpen)
            {
                Sector(split.left, false);
                Sector(split.right, true);
                TreePop();
            }
        }

        void Sector(CollisionSector sector, bool isRight)
        {
            if (sector.count == RWCollision.SplitCount)
                Split(sector.index);
            else
                Text($"{(isRight ? "Right" : "Left")}: {sector.count} Triangles");
        }
    }

    private void HandleRaycast()
    {
        if (worldCollider == null)
        {
            Text("No world or collider loaded");
            return;
        }

        Spacing();
        Text("Intersections");
        var shouldUpdate = ImGuiEx.EnumCombo("Primitive", ref intersectionPrimitive);
        if (intersectionPrimitive is IntersectionPrimitive.Ray)
        {
            shouldUpdate |= Checkbox("Set to camera", ref setRaycastToCamera);
            BeginDisabled(setRaycastToCamera);
            shouldUpdate |= DragFloat3("Start", ref raycastStart);
            shouldUpdate |= DragFloat3("Direction", ref raycastDir);
            EndDisabled();
        }
        else
        {
            shouldUpdate |= SliderFloat("Size", ref intersectionSize, 0.01f, 20f);
        }
        shouldUpdate |= Checkbox("Update location", ref updateIntersectionPrimitive);
        if (updateIntersectionPrimitive || shouldUpdate)
        {
            if (intersectionPrimitive == IntersectionPrimitive.Ray)
                ShootRay();
            else
                UpdateIntersectionPrimitive();
        }
    }

    private void ShootRay()
    {
        if (worldCollider == null)
            return;
        if (setRaycastToCamera)
        {
            raycastStart = camera.Location.GlobalPosition;
            raycastDir = -camera.Location.GlobalForward;
        }

        var ray = new Ray(raycastStart, raycastDir);
        var cast = worldCollider.Cast(ray);
        rayRenderer.Clear();
        rayRenderer.Add(IColor.Green, ray.Start, ray.Start + ray.Direction * (cast?.Distance ?? 100f));
        if (cast.HasValue)
        {
            if (cast.Value.TriangleId.HasValue)
            {
                var triInfo = worldCollider.GetTriangleInfo(cast.Value.TriangleId.Value);
                rayRenderer.Add(IColor.Green, triInfo.Triangle.Edges());
            }
            rayRenderer.Add(new IColor(255, 0, 255, 255), cast.Value.Point, cast.Value.Point + cast.Value.Normal * 0.2f);
        }
        fbArea.IsDirty = true;
    }

    private void UpdateIntersectionPrimitive()
    {
        if (!updateIntersectionPrimitive || worldCollider == null)
            return;

        Vector3 center = camera.Location.GlobalPosition;
        IEnumerable<Intersection> intersections;
        IEnumerable<Line> edges;
        rayRenderer.Clear();
        switch (intersectionPrimitive)
        {
            case IntersectionPrimitive.Box:
                var box = new Box(center, Vector3.One * intersectionSize);
                intersections = worldCollider.Intersections(box);
                edges = box.Edges();
                break;

            case IntersectionPrimitive.OrientedBox:
                var orientedBox = new OrientedBox(
                    new Box(center, Vector3.One * intersectionSize),
                    camera.Location.GlobalRotation);
                intersections = worldCollider.Intersections(orientedBox);
                edges = orientedBox.Edges();
                break;

            case IntersectionPrimitive.Sphere:
                var sphere = new Sphere(center, intersectionSize);
                intersections = worldCollider.Intersections(sphere);
                edges = sphere.Edges();
                break;

            case IntersectionPrimitive.Triangle:
                var hh = intersectionSize * MathF.Sqrt(3f) / 4f;
                var (right, up) = (camera.Location.GlobalRight, camera.Location.GlobalUp);
                var triangle = new Triangle(
                    center - right * intersectionSize / 2f - up * hh,
                    center + right * intersectionSize / 2f - up * hh,
                    center + up * hh);
                intersections = worldCollider.Intersections(triangle);
                edges = triangle.Edges();
                break;

            case IntersectionPrimitive.Line:
                var pos = camera.Location.GlobalPosition;
                var line = new Line(pos, pos - camera.Location.GlobalForward * intersectionSize);
                intersections = worldCollider.Intersections(line);
                edges = new[] { line };
                break;

            default: return;
        }

        rayRenderer.Add(
            intersections.Any() ? IColor.Red : IColor.Green,
            edges);
        foreach (var intersection in intersections)
        {
            var p1 = intersection.Point;
            var p2 = p1 + intersection.Normal * 0.1f;
            rayRenderer.Add(IColor.Green, intersection.Triangle.Edges());
            rayRenderer.Add(IColor.Blue, p1, p2);
        }

        fbArea.IsDirty = true;
    }
}
