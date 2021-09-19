using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Veldrid;
using zzio.primitives;
using zzio.rwbs;
using zzio.utils;
using zzio.vfs;
using zzre.core;
using zzre.debug;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using static ImGuiNET.ImGui;

namespace zzre.tools
{
    public class WorldViewer : ListDisposable, IDocumentEditor
    {
        private const byte DebugPlaneAlpha = 0xA0;

        private readonly ITagContainer diContainer;
        private readonly TwoColumnEditorTag editor;
        private readonly FlyControlsTag controls;
        private readonly FramebufferArea fbArea;
        private readonly IResourcePool resourcePool;
        private readonly OpenFileModal openFileModal;
        private readonly ModelMaterialEdit modelMaterialEdit;
        private readonly DebugBoxLineRenderer boundsRenderer;
        private readonly DebugBoxLineRenderer rayRenderer;
        private readonly DebugPlaneRenderer planeRenderer;
        private readonly DebugHexahedronLineRenderer frustumRenderer;
        private readonly DebugTriangleLineRenderer triangleRenderer;
        private readonly WorldRenderer worldRenderer;
        private readonly Camera camera;
        private readonly LocationBuffer locationBuffer;

        private Frustum viewFrustum => worldRenderer.ViewFrustum;
        private IReadOnlyList<ModelStandardMaterial> materials => worldRenderer.Materials;

        private readonly UniformBuffer<Matrix4x4> worldTransform;
        private WorldBuffers? worldBuffers;
        private AtomicCollider? atomicCollider;
        private int[] sectionDepths = new int[0];
        private int highlightedSectionI = -1;
        private int highlightedSplitI = -1;
        private bool updateViewFrustumCulling = true;
        private bool renderCulledSections = false;
        private bool renderOnlyCollision = false;

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
            var menuBar = new MenuBarWindowTag(Window);
            menuBar.AddButton("Open", HandleMenuOpen);
            var gridRenderer = new DebugGridRenderer(diContainer);
            //gridRenderer.Material.LinkTransformsTo(controls.Projection, controls.View, controls.World);
            AddDisposable(gridRenderer);
            modelMaterialEdit = new ModelMaterialEdit(Window, diContainer);
            modelMaterialEdit.OpenEntriesByDefault = false;
            diContainer.GetTag<OpenDocumentSet>().AddEditor(this);
            openFileModal = new OpenFileModal(diContainer);
            openFileModal.Filter = "*.bsp";
            openFileModal.IsFilterChangeable = false;
            openFileModal.OnOpenedResource += Load;

            locationBuffer = new LocationBuffer(diContainer.GetTag<GraphicsDevice>());
            AddDisposable(locationBuffer);
            camera = new Camera(diContainer.ExtendedWith(locationBuffer));
            AddDisposable(camera);
            controls = new FlyControlsTag(Window, camera.Location, diContainer);
            worldTransform = new UniformBuffer<Matrix4x4>(diContainer.GetTag<GraphicsDevice>().ResourceFactory);
            worldTransform.Ref = Matrix4x4.Identity;
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
            editor.AddInfoSection("Collision", HandleCollision, true);

            boundsRenderer = new DebugBoxLineRenderer(diContainer);
            boundsRenderer.Color = IColor.Red;
            boundsRenderer.Material.LinkTransformsTo(camera);
            boundsRenderer.Material.LinkTransformsTo(world: worldTransform);
            AddDisposable(boundsRenderer);

            planeRenderer = new DebugPlaneRenderer(diContainer);
            planeRenderer.Material.LinkTransformsTo(camera);
            planeRenderer.Material.LinkTransformsTo(world: worldTransform);
            AddDisposable(planeRenderer);

            frustumRenderer = new DebugHexahedronLineRenderer(diContainer);
            frustumRenderer.Material.LinkTransformsTo(camera);
            frustumRenderer.Material.LinkTransformsTo(world: worldTransform);
            AddDisposable(frustumRenderer);

            triangleRenderer = new DebugTriangleLineRenderer(diContainer);
            triangleRenderer.Material.LinkTransformsTo(camera);
            triangleRenderer.Material.LinkTransformsTo(world: worldTransform);
            AddDisposable(triangleRenderer);

            rayRenderer = new DebugBoxLineRenderer(diContainer);
            rayRenderer.Color = IColor.Green;
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

            worldBuffers = new WorldBuffers(diContainer, resource);
            AddDisposable(worldBuffers);
            worldRenderer.WorldBuffers = worldBuffers;
            modelMaterialEdit.Materials = materials;

            CurrentResource = resource;
            UpdateSectionDepths();
            HighlightSection(-1);
            controls.ResetView();
            camera.Location.LocalPosition = -worldBuffers.Origin;
            fbArea.IsDirty = true;
            Window.Title = $"World Viewer - {resource.Path.ToPOSIXString()}";

            HighlightSection(worldBuffers.Sections.Indexed().First(s => s.Value.IsMesh).Index);
            renderOnlyCollision = true;
        }

        private void UpdateSectionDepths()
        {
            // the section depth makes the ImGui tree content much easier
            if (worldBuffers == null)
                throw new InvalidOperationException();
            sectionDepths = new int[worldBuffers.Sections.Count];
            foreach (var (section, index) in worldBuffers.Sections.Indexed())
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
            if (worldBuffers == null)
                return;

            if (updateViewFrustumCulling)
            {
                worldRenderer.UpdateVisibility();
            }

            if (renderOnlyCollision)
            {
                if (highlightedSectionI >= 0 && worldBuffers.Sections[highlightedSectionI] is WorldBuffers.MeshSection meshSection)
                    worldRenderer.Render(cl, new[] { meshSection });
            }
            else if (renderCulledSections)
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

            if (atomicCollider != null && atomicCollider.Trace.Any())
                rayRenderer.Render(cl);

            triangleRenderer.Render(cl);
        }

        private void HandleResize() => camera.Aspect = fbArea.Ratio;

        private void HandleMenuOpen()
        {
            openFileModal.InitialSelectedResource = CurrentResource;
            openFileModal.Modal.Open();
        }

        private void HandleStatisticsContent()
        {
            Text($"Vertices: {worldBuffers?.VertexCount ?? 0}");
            Text($"Triangles: {worldBuffers?.TriangleCount ?? 0}");
            Text($"Planes: {worldBuffers?.Sections.Count(s => s.IsPlane) ?? 0}");
            Text($"Atomics: {worldBuffers?.Sections.Count(s => s.IsMesh) ?? 0}");
            Text($"SubMeshes: {worldBuffers?.SubMeshes.Count ?? 0}");
            Text($"Materials: {worldBuffers?.Materials.Count ?? 0}");
        }

        private void HandleMaterialsContent()
        {
            if (worldBuffers == null)
                return;
            else if (modelMaterialEdit.Content())
                fbArea.IsDirty = true;
        }

        private void HandleSectionsContent()
        {
            if (worldBuffers == null)
                return;

            if (Button("Clear selection"))
            {
                HighlightSection(-1);
                fbArea.IsDirty = true;
            }

            int curDepth = 0;
            foreach (var (section, index) in worldBuffers.Sections.Indexed())
            {
                if (curDepth < sectionDepths[index])
                    continue;
                while (curDepth > sectionDepths[index])
                {
                    TreePop();
                    curDepth--;
                }

                if (section.IsMesh)
                    MeshSectionContent((WorldBuffers.MeshSection)section, index);
                else
                    PlaneSectionContent((WorldBuffers.PlaneSection)section, index);
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

            void MeshSectionContent(WorldBuffers.MeshSection section, int index)
            {
                bool isVisible = worldRenderer.VisibleMeshSections.Contains(section);
                Text(isVisible ? IconFonts.ForkAwesome.Eye : IconFonts.ForkAwesome.EyeSlash);

                SameLine();
                if (!SectionHeaderContent($"MeshSection #{index}", index))
                    return;
                Text($"Vertices: {section.VertexCount}");
                Text($"Triangles: {section.TriangleCount}");
                Text($"SubMeshes: {section.SubMeshes.GetLength(worldBuffers.SubMeshes.Count)}");
            }

            void PlaneSectionContent(WorldBuffers.PlaneSection section, int index)
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
            renderOnlyCollision = false;
            atomicCollider = null;
            if (worldBuffers == null || highlightedSectionI < 0)
                return;
            boundsRenderer.Bounds = worldBuffers.Sections[index].Bounds;
            planeRenderer.Planes = new DebugPlane[0];

            if (worldBuffers.Sections[index].IsPlane)
            {
                var section = (WorldBuffers.PlaneSection)worldBuffers.Sections[index];
                SetPlanes(section.Bounds, section.PlaneType.AsNormal().ToNumerics(), section.LeftValue, section.RightValue, section.CenterValue);
            }
            else
            {
                var worldSection = (WorldBuffers.MeshSection)worldBuffers.Sections[index];
                var atomicSection = worldSection.RWAtomicSection;
                if (atomicSection.FindChildById(SectionId.CollisionPLG, recursive: true) != null)
                    atomicCollider = new AtomicCollider(atomicSection);
            }

            fbArea.IsDirty = true;
        }

        private void HighlightSplit(int splitI)
        {
            highlightedSplitI = splitI;
            triangleRenderer.Triangles = Array.Empty<Triangle>();
            if (atomicCollider == null || splitI < 0)
                return;

            var split = atomicCollider.Collision.splits[splitI];
            var normal = split.left.type switch
            {
                CollisionSectorType.X => Vector3.UnitX,
                CollisionSectorType.Y => Vector3.UnitY,
                CollisionSectorType.Z => Vector3.UnitZ,
                _ => throw new NotSupportedException($"Unsupported collision sector type: " + split.left.type)
            };
            SetPlanes(atomicCollider.Box, normal, split.left.value, split.right.value, centerValue: null);

            triangleRenderer.Triangles = SplitTriangles(split).ToArray();

            fbArea.IsDirty = true;

            IEnumerable<Triangle> SplitTriangles(CollisionSplit split) =>
                SectorTriangles(split.left).Concat(SectorTriangles(split.right));

            IEnumerable<Triangle> SectorTriangles(CollisionSector sector) => sector.count == RWCollision.SplitCount
                ? SplitTriangles(atomicCollider.Collision.splits[sector.index])
                : atomicCollider.Collision.map
                    .Skip(sector.index)
                    .Take(sector.count)
                    .Select(atomicCollider.GetTriangle)
                    .ToArray();
        }

        private void SetPlanes(Box bounds, Vector3 normal, float leftValue, float rightValue, float? centerValue)
        {
            var planarCenter = bounds.Center * (Vector3.One - normal);
            var otherSizes = bounds.Size * (Vector3.One - normal);
            var size = Math.Max(Math.Max(otherSizes.X, otherSizes.Y), otherSizes.Z) * 0.5f;
            planeRenderer.Planes = new[]
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
                planeRenderer.Planes = planeRenderer.Planes.Append(
                    new DebugPlane()
                    {
                        center = planarCenter + normal * centerValue.Value,
                        normal = normal,
                        size = size,
                        color = IColor.Green.WithA(DebugPlaneAlpha)
                    }).ToArray();
            }
        }

        private void HandleViewFrustumCulling()
        {
            Text($"Visible meshes: {worldRenderer.VisibleMeshSections.Count}/{worldBuffers?.Sections.OfType<WorldBuffers.MeshSection>().Count() ?? 0}");
            var visibleTriangleCount = worldRenderer.VisibleMeshSections.Sum(s => s.TriangleCount);
            Text($"Visible triangles: {visibleTriangleCount}/{worldBuffers?.TriangleCount ?? 0}");
            NewLine();

            bool didChange = false;
            didChange |= Checkbox("Update ViewFrustum", ref updateViewFrustumCulling);
            didChange |= Checkbox("Render culled sections", ref renderCulledSections);

            if (didChange)
            {
                viewFrustum.Corners.ToArray().CopyTo(frustumRenderer.Corners, 0);
                fbArea.IsDirty = true;
            }
        }

        private void HandleCollision()
        {
            if (worldBuffers == null)
            {
                Text("No world loaded");
                return;
            }
            else if (highlightedSectionI < 0)
            {
                Text("No section selected");
                return;
            }
            else if (atomicCollider == null)
            {
                Text("No collision in selected section");
                return;
            }

#if DEBUG_TREE_COLLIDER
            if (Button("Shoot ray"))
                ShootRay();
#endif

            var coll = atomicCollider.Collision;
            fbArea.IsDirty |= Checkbox("Only render collision", ref renderOnlyCollision);
            Split(0);

            void Split(int splitI)
            {
                var split = coll.splits[splitI];
                var traceIcons = "";
#if DEBUG_TREE_COLLIDER
                var traceFlags = atomicCollider.Trace.FirstOrDefault(t => t.split == splitI).flags;
                if (traceFlags.HasFlag(TreeTraceFlags.Hit))
                    traceIcons += " H";
                if (traceFlags.HasFlag(TreeTraceFlags.TookBothBranches))
                    traceIcons += "B";
                if (traceFlags.HasFlag(TreeTraceFlags.TookLeftFirst))
                    traceIcons += "L";
#endif

                var flags = (splitI == highlightedSplitI ? ImGuiTreeNodeFlags.Selected : 0) |
                    ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.OpenOnArrow |
                    ImGuiTreeNodeFlags.DefaultOpen;
                var isOpen = TreeNodeEx($"{split.left.type} {split.left.value}-{split.right.value}{traceIcons}", flags);
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

#if DEBUG_TREE_COLLIDER
        private void ShootRay()
        {
            if (atomicCollider == null)
                return;

            var ray = new Ray(camera.Location.GlobalPosition, camera.Location.GlobalForward);
            _ = atomicCollider.Cast(ray);
            rayRenderer.Bounds = new OrientedBox(
                Box.FromMinMax(ray.Start, ray.Start + Vector3.UnitZ * 100f),
                System.Numerics.Quaternion.CreateFromRotationMatrix(
                    Matrix4x4.CreateLookAt(Vector3.Zero, ray.Direction, Vector3.UnitY)));
            rayRenderer.Bounds = new OrientedBox(
                new Box(ray.Start, Vector3.One),
                System.Numerics.Quaternion.Identity);
            fbArea.IsDirty = true;
        }
#endif
    }
}
