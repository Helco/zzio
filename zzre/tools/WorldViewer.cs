using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Veldrid;
using zzio;
using zzio.rwbs;
using zzio.vfs;
using zzre.debug;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using static ImGuiNET.ImGui;

namespace zzre.tools;

public class WorldViewer : ListDisposable, IDocumentEditor
{
    private enum IntersectionPrimitive
    {
        None,
        Box,
        OrientedBox,
        Sphere,
        Triangle
    }

    private const byte DebugPlaneAlpha = 0xA0;

    private readonly ITagContainer diContainer;
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
    private readonly WorldRenderer worldRenderer;
    private readonly Camera camera;
    private readonly LocationBuffer locationBuffer;

    private Frustum viewFrustum => worldRenderer.ViewFrustum;
    private IReadOnlyList<ModelMaterial> materials => worldRenderer.Materials;

    private readonly UniformBuffer<Matrix4x4> worldTransform;
    private WorldMesh? worldMesh;
    private WorldCollider? worldCollider;
    private RWAtomicSection? sectionAtomic;
    private RWCollision? sectionCollision;
    private int[] sectionDepths = Array.Empty<int>();
    private int highlightedSectionI = -1;
    private int highlightedSplitI = -1;
    private bool updateViewFrustumCulling = true;
    private bool renderCulledSections = false;
    private IntersectionPrimitive intersectionPrimitive;
    private bool updateIntersectionPrimitive;
    private float intersectionSize = 0.5f;

    public IResource? CurrentResource { get; private set; }
    public Window Window { get; }

    public WorldViewer(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
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

        worldRenderer = new WorldRenderer(diContainer.ExtendedWith(camera, locationBuffer));
        AddDisposable(worldRenderer);
    }

    public void Load(string pathText)
    {
        var resource = resourcePool.FindFile(pathText);
        if (resource == null)
            throw new FileNotFoundException($"Could not find world at {pathText}");
        Load(resource);
    }

    public void Load(IResource resource) =>
        Window.GetTag<OnceAction>().Next += () => LoadWorldNow(resource);

    private void LoadWorldNow(IResource resource)
    {
        if (resource.Equals(CurrentResource))
            return;
        CurrentResource = null;

        worldMesh = new WorldMesh(diContainer, resource);
        AddDisposable(worldMesh);
        worldRenderer.WorldMesh = worldMesh;
        modelMaterialEdit.Materials = materials;

        CurrentResource = resource;
        UpdateSectionDepths();
        worldCollider = new WorldCollider(worldMesh.World);
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
        foreach (var (section, index) in worldMesh.Sections.Indexed())
        {
            int depth = 0;
            var curSection = section;
            while (curSection.Parent != null)
            {
                depth++;
                curSection = curSection.Parent;
            }
            sectionDepths[index] = depth;
        }
    }

    private void HandleRender(CommandList cl)
    {
        if (worldMesh == null)
            return;

        if (updateViewFrustumCulling)
        {
            worldRenderer.UpdateVisibility();
        }

        if (renderCulledSections)
            worldRenderer.RenderForceAll(cl);
        else
            worldRenderer.Render(cl);

        if (highlightedSectionI >= 0)
        {
            boundsRenderer.Render(cl);
            planeRenderer.Render(cl);
        }

        if (!updateViewFrustumCulling)
            frustumRenderer.Render(cl);

        triangleRenderer.Render(cl);
        rayRenderer.Render(cl);
    }

    private void HandleResize() => camera.Aspect = fbArea.Ratio;

    private void HandleKeyDown(Key key)
    {
        if (key == Key.Space)
            ShootRay();
    }

    private void HandleMenuOpen()
    {
        openFileModal.InitialSelectedResource = CurrentResource;
        openFileModal.Modal.Open();
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
        foreach (var (section, index) in worldMesh.Sections.Indexed())
        {
            if (curDepth < sectionDepths[index])
                continue;
            while (curDepth > sectionDepths[index])
            {
                TreePop();
                curDepth--;
            }

            if (section is WorldMesh.MeshSection meshSection)
                MeshSectionContent(meshSection, index);
            else if (section is WorldMesh.PlaneSection planeSection)
                PlaneSectionContent(planeSection, index);
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
            Text($"SubMeshes: {worldMesh.GetSubMeshes(section.SubMeshSection).Count()}");
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
        planeRenderer.Planes = Array.Empty<DebugPlane>();

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

        triangleRenderer.AddTriangles(
            triangles: SplitTriangles(split).ToArray(),
            colors: Enumerable.Repeat(IColor.Red, SectorTriangles(split.left).Count())
            .Concat(Enumerable
                .Repeat(IColor.Blue, SectorTriangles(split.right).Count()))
            .ToArray());

        fbArea.IsDirty = true;

        IEnumerable<Triangle> SplitTriangles(CollisionSplit split) =>
            SectorTriangles(split.left).Concat(SectorTriangles(split.right));

        IEnumerable<Triangle> SectorTriangles(CollisionSector sector) => sector.count == RWCollision.SplitCount
            ? SplitTriangles(sectionCollision.splits[sector.index])
            : sectionCollision.map
                .Skip(sector.index)
                .Take(sector.count)
                .Select(i => sectionAtomic.triangles[i])
                .Select(t => new Triangle(
                    sectionAtomic.vertices[t.v1],
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

    private void HandleViewFrustumCulling()
    {
        Text($"Visible meshes: {worldRenderer.VisibleMeshSections.Count}/{worldMesh?.Sections.OfType<WorldMesh.MeshSection>().Count() ?? 0}");
        var visibleTriangleCount = worldRenderer.VisibleMeshSections.Sum(s => s.TriangleCount);
        Text($"Visible triangles: {visibleTriangleCount}/{worldMesh?.TriangleCount ?? 0}");
        NewLine();

        bool didChange = false;
        didChange |= Checkbox("Update ViewFrustum", ref updateViewFrustumCulling);
        didChange |= Checkbox("Render culled sections", ref renderCulledSections);

        if (didChange)
        {
            frustumRenderer.Clear();
            frustumRenderer.AddHexahedron(viewFrustum.Corners, IColor.White);
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

        if (Button("Shoot ray"))
            ShootRay();

        Spacing();
        Text("Intersections");
        var shouldUpdate = ImGuiEx.EnumCombo("Primitive", ref intersectionPrimitive);
        shouldUpdate |= SliderFloat("Size", ref intersectionSize, 0.01f, 20f);
        shouldUpdate |= Checkbox("Update location", ref updateIntersectionPrimitive);
        if (shouldUpdate)
            UpdateIntersectionPrimitive();
    }

    private void ShootRay()
    {
        if (worldCollider == null)
            return;

        var ray = new Ray(camera.Location.GlobalPosition, -camera.Location.GlobalForward);
        var cast = worldCollider.Cast(ray);
        rayRenderer.Clear();
        rayRenderer.Reserve(5, additive: false);
        rayRenderer.Add(IColor.Green, ray.Start, ray.Start + ray.Direction * (cast?.Distance ?? 100f));
        if (cast.HasValue)
        {
            rayRenderer.Add(IColor.Green, worldCollider.LastTriangle.Edges());
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
