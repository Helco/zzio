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
        private readonly TextureLoader textureLoader;
        private readonly TwoColumnEditorTag editor;
        private readonly FlyControlsTag controls;
        private readonly FramebufferArea fbArea;
        private readonly IResourcePool resourcePool;
        private readonly OpenFileModal openFileModal;
        private readonly ModelMaterialEdit modelMaterialEdit;
        private readonly DebugBoundsLineRenderer boundsRenderer;
        private readonly DebugPlaneRenderer planeRenderer;
        private readonly DebugHexahedronLineRenderer frustumRenderer;
        private readonly WorldRenderer worldRenderer;

        private ViewFrustumCulling viewFrustumCulling => worldRenderer.ViewFrustumCulling;
        private IReadOnlyList<ModelStandardMaterial> materials => worldRenderer.Materials;

        private UniformBuffer<Matrix4x4> worldTransform;
        private RWWorldBuffers? worldBuffers;
        private int[] sectionDepths = new int[0];
        private int highlightedSectionI = -1;
        private bool updateViewFrustumCulling = true;
        private bool renderCulledSections = false;

        public IResource? CurrentResource { get; private set; }
        public Window Window { get; }

        public WorldViewer(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            textureLoader = diContainer.GetTag<TextureLoader>();
            resourcePool = diContainer.GetTag<IResourcePool>();
            Window = diContainer.GetTag<WindowContainer>().NewWindow("World Viewer");
            Window.AddTag(this);
            Window.InitialBounds = new Rect(float.NaN, float.NaN, 1100.0f, 600.0f);
            editor = new TwoColumnEditorTag(Window, diContainer);
            controls = new FlyControlsTag(Window, diContainer);
            var onceAction = new OnceAction();
            Window.AddTag(onceAction);
            Window.OnContent += onceAction.Invoke;
            var menuBar = new MenuBarWindowTag(Window);
            menuBar.AddItem("Open", HandleMenuOpen);
            var gridRenderer = new DebugGridRenderer(diContainer);
            gridRenderer.Material.LinkTransformsTo(controls.Projection, controls.View, controls.World);
            AddDisposable(gridRenderer);
            fbArea = Window.GetTag<FramebufferArea>();
            fbArea.OnRender += gridRenderer.Render;
            fbArea.OnRender += HandleRender;
            modelMaterialEdit = new ModelMaterialEdit(Window, diContainer);
            modelMaterialEdit.OpenEntriesByDefault = false;
            diContainer.GetTag<OpenDocumentSet>().AddEditor(this);

            openFileModal = new OpenFileModal(diContainer);
            openFileModal.Filter = "*.bsp";
            openFileModal.IsFilterChangeable = false;
            openFileModal.OnOpenedResource += Load;

            editor.AddInfoSection("Statistics", HandleStatisticsContent);
            editor.AddInfoSection("Materials", HandleMaterialsContent, false);
            editor.AddInfoSection("Sections", HandleSectionsContent, false);
            editor.AddInfoSection("ViewFrustum Culling", HandleViewFrustumCulling, false);

            worldTransform = new UniformBuffer<Matrix4x4>(diContainer.GetTag<GraphicsDevice>().ResourceFactory);
            worldTransform.Ref = Matrix4x4.Identity;
            AddDisposable(worldTransform);

            boundsRenderer = new DebugBoundsLineRenderer(diContainer);
            boundsRenderer.Color = IColor.Red;
            boundsRenderer.Material.LinkTransformsTo(controls.Projection, controls.View, worldTransform);
            AddDisposable(boundsRenderer);

            planeRenderer = new DebugPlaneRenderer(diContainer);
            planeRenderer.Material.LinkTransformsTo(controls.Projection, controls.View, worldTransform);
            AddDisposable(planeRenderer);

            frustumRenderer = new DebugHexahedronLineRenderer(diContainer);
            frustumRenderer.Material.LinkTransformsTo(controls.Projection, controls.View, controls.World);
            AddDisposable(frustumRenderer);

            worldRenderer = new WorldRenderer(diContainer.ExtendedWith<IStandardTransformMaterial>(boundsRenderer.Material));
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

            worldBuffers = new RWWorldBuffers(diContainer, resource);
            AddDisposable(worldBuffers);
            worldRenderer.WorldBuffers = worldBuffers;
            modelMaterialEdit.Materials = materials;

            CurrentResource = resource;
            UpdateSectionDepths();
            highlightedSectionI = -1;
            controls.ResetView();
            controls.Position = worldBuffers.Origin;
            fbArea.IsDirty = true;
            Window.Title = $"World Viewer - {resource.Path.ToPOSIXString()}";
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
            worldTransform.Update(cl);
            if (worldBuffers == null)
                return;

            if (updateViewFrustumCulling)
            {
                viewFrustumCulling.SetViewProjection(controls.View.Value, controls.Projection.Value);
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
        }

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
                    MeshSectionContent((RWWorldBuffers.MeshSection)section, index);
                else
                    PlaneSectionContent((RWWorldBuffers.PlaneSection)section, index);
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

            void MeshSectionContent(RWWorldBuffers.MeshSection section, int index)
            {
                bool isVisible = worldRenderer.VisibleMeshSections.Contains(section);
                Text(isVisible ? IconFonts.ForkAwesome.Eye : IconFonts.ForkAwesome.EyeSlash);

                SameLine();
                if (!SectionHeaderContent($"MeshSection #{index}", index))
                    return;
                Text($"Vertices: {section.VertexCount}");
                Text($"Triangles: {section.TriangleCount}");
                Text($"SubMeshes: {section.SubMeshCount}");
            }

            void PlaneSectionContent(RWWorldBuffers.PlaneSection section, int index)
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
            if (worldBuffers == null || highlightedSectionI < 0)
                return;
            boundsRenderer.Bounds = worldBuffers.Sections[index].Bounds;
            planeRenderer.Planes = new DebugPlane[0];

            if (worldBuffers.Sections[index].IsPlane)
            {
                var section = (RWWorldBuffers.PlaneSection)worldBuffers.Sections[index];
                var normal = section.PlaneType.AsNormal().ToNumerics();
                var planarCenter = section.Bounds.Center * (Vector3.One - normal);
                var otherSizes = section.Bounds.Size * (Vector3.One - normal);
                var size = Math.Max(Math.Max(otherSizes.X, otherSizes.Y), otherSizes.Z) * 0.5f;
                planeRenderer.Planes = new[]
                {
                    new DebugPlane()
                    {
                        center = planarCenter + normal * section.CenterValue,
                        normal = normal,
                        size = size,
                        color = IColor.Green.WithA(DebugPlaneAlpha)
                    },
                    new DebugPlane()
                    {
                        center = planarCenter + normal * section.LeftValue,
                        normal = normal,
                        size = size * 0.7f,
                        color = IColor.Red.WithA(DebugPlaneAlpha)
                    },
                    new DebugPlane()
                    {
                        center = planarCenter + normal * section.RightValue,
                        normal = normal,
                        size = size * 0.7f,
                        color = IColor.Blue.WithA(DebugPlaneAlpha)
                    }
                };
            }

            fbArea.IsDirty = true;
        }

        private void HandleViewFrustumCulling()
        {
            Text($"Visible meshes: {worldRenderer.VisibleMeshSections.Count}/{worldBuffers?.Sections.OfType<RWWorldBuffers.MeshSection>().Count() ?? 0}");
            var visibleTriangleCount = worldRenderer.VisibleMeshSections.Sum(s => s.TriangleCount);
            Text($"Visible triangles: {visibleTriangleCount}/{worldBuffers?.TriangleCount ?? 0}");
            NewLine();

            bool didChange = false;
            didChange |= Checkbox("Update ViewFrustum", ref updateViewFrustumCulling);
            didChange |= Checkbox("Render culled sections", ref renderCulledSections);

            if (didChange)
            {
                viewFrustumCulling.FrustumCorners.ToArray().CopyTo(frustumRenderer.Corners, 0);
                fbArea.IsDirty = true;
            }
        }
    }
}
