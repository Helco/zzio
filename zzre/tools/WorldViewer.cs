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
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using static ImGuiNET.ImGui;

namespace zzre.tools
{
    public class WorldViewer : ListDisposable, IDocumentEditor
    {
        private readonly ITagContainer diContainer;
        private readonly TextureLoader textureLoader;
        private readonly TwoColumnEditorTag editor;
        private readonly FlyControlsTag controls;
        private readonly FramebufferArea fbArea;
        private readonly IResourcePool resourcePool;
        private readonly OpenFileModal openFileModal;
        private readonly ModelMaterialEdit modelMaterialEdit;
        private readonly DebugBoundsRenderer boundsRenderer;

        private UniformBuffer<Matrix4x4> worldTransform;
        private RWWorldBuffers? worldBuffers;
        private ModelStandardMaterial[] materials = new ModelStandardMaterial[0];
        private int[] sectionDepths = new int[0];
        private bool[] sectionVisibility = new bool[0];
        private int highlightedSectionI = -1;

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
            new DeferredCallerTag(Window);
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
            openFileModal.OnOpenedResource += LoadWorld;

            editor.AddInfoSection("Statistics", HandleStatisticsContent);
            editor.AddInfoSection("Materials", HandleMaterialsContent, false);
            editor.AddInfoSection("Sections", HandleSectionsContent, false);

            worldTransform = new UniformBuffer<Matrix4x4>(diContainer.GetTag<GraphicsDevice>().ResourceFactory);
            worldTransform.Ref = Matrix4x4.Identity;
            AddDisposable(worldTransform);

            boundsRenderer = new DebugBoundsRenderer(diContainer);
            boundsRenderer.Color = IColor.Red;
            boundsRenderer.Material.LinkTransformsTo(controls.Projection, controls.View, worldTransform);
            AddDisposable(boundsRenderer);
        }

        public static WorldViewer OpenFor(ITagContainer diContainer, string pathText)
        {
            var resourcePool = diContainer.GetTag<IResourcePool>();
            var resource = resourcePool.FindFile(pathText);
            if (resource == null)
                throw new FileNotFoundException($"Could not find world at {pathText}");
            return OpenFor(diContainer, resource);
        }

        public static WorldViewer OpenFor(ITagContainer diContainer, IResource resource)
        {
            var openDocumentSet = diContainer.GetTag<OpenDocumentSet>();
            if (openDocumentSet.TryGetEditorFor(resource, out var prevEditor))
            {
                prevEditor.Window.Focus();
                return (WorldViewer)prevEditor;
            }
            var newEditor = new WorldViewer(diContainer);
            newEditor.LoadWorld(resource);
            return newEditor;
        }

        public void LoadWorld(string pathText)
        {
            var resource = resourcePool.FindFile(pathText);
            if (resource == null)
                throw new FileNotFoundException($"Could not find world at {pathText}");
            LoadWorld(resource);
        }

        public void LoadWorld(IResource resource) =>
            Window.GetTag<DeferredCallerTag>().Next += () => LoadWorldNow(resource);

        private void LoadWorldNow(IResource resource)
        {
            if (resource.Equals(CurrentResource))
                return;
            CurrentResource = null;

            using var contentStream = resource.OpenContent();
            if (contentStream == null)
                throw new IOException($"Could not open model at {resource.Path.ToPOSIXString()}");
            var rwWorld = Section.ReadNew(contentStream) as RWWorld;
            if (rwWorld?.sectionId != SectionId.World)
                throw new InvalidDataException($"Expected a root world section got a {rwWorld?.sectionId.ToString() ?? "read error"}");

            worldBuffers = new RWWorldBuffers(diContainer, rwWorld);
            AddDisposable(worldBuffers);

            var textureBase = new FilePath("resources/textures/worlds");
            materials = new ModelStandardMaterial[worldBuffers.Materials.Count];
            foreach (var (rwMaterial, index) in worldBuffers.Materials.Indexed())
            {
                var material = materials[index] = new ModelStandardMaterial(diContainer);
                (material.MainTexture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(textureBase, rwMaterial);
                material.LinkTransformsTo(controls.Projection, controls.View, worldTransform);
                material.Uniforms.Ref = ModelStandardMaterialUniforms.Default;
                AddDisposable(material);
            }
            modelMaterialEdit.Materials = materials;

            worldTransform.Ref = Matrix4x4.CreateTranslation(rwWorld.origin.ToNumerics());

            CurrentResource = resource;
            UpdateSectionDepths();
            sectionVisibility = Enumerable.Repeat(true, worldBuffers.Sections.Count).ToArray();
            highlightedSectionI = -1;
            controls.ResetView();
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
            worldBuffers.SetBuffers(cl);
            foreach (var (section, index) in worldBuffers.Sections.Indexed().Where(t => t.Value.IsMesh))
            {
                if (!sectionVisibility[index])
                    continue; // not as where clause for future visibility methods

                var meshSection = (RWWorldBuffers.MeshSection)section;
                foreach (var subMesh in worldBuffers.SubMeshes.Skip(meshSection.SubMeshStart).Take(meshSection.SubMeshCount))
                {
                    (materials[subMesh.MaterialIndex] as IMaterial).Apply(cl);
                    cl.DrawIndexed(
                        indexStart: (uint)subMesh.IndexOffset,
                        indexCount: (uint)subMesh.IndexCount,
                        instanceCount: 1,
                        vertexOffset: 0,
                        instanceStart: 0);
                }
            }

            if (highlightedSectionI >= 0)
                boundsRenderer.Render(cl);
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
                PushID(index);
                bool isVisible = sectionVisibility[index];
                if (SmallButton(isVisible ? IconFonts.ForkAwesome.Eye : IconFonts.ForkAwesome.EyeSlash))
                {
                    sectionVisibility[index] = !isVisible;
                    fbArea.IsDirty = true;
                }
                PopID();

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
            if (worldBuffers == null)
                return;
            boundsRenderer.Bounds = worldBuffers.Sections[index].Bounds;
            // TODO: Visualize highlighted world section planes better (center, left, right)
            fbArea.IsDirty = true;
        }
    }
}
