using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Veldrid;
using zzio.rwbs;
using zzio.utils;
using zzio.vfs;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;

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

        private UniformBuffer<Matrix4x4> worldTransform;
        private RWWorldBuffers? worldBuffers;
        private ModelStandardMaterial[] materials = new ModelStandardMaterial[0];

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
            fbArea = Window.GetTag<FramebufferArea>();
            fbArea.OnRender += HandleRender;
            diContainer.GetTag<OpenDocumentSet>().AddEditor(this);

            openFileModal = new OpenFileModal(diContainer);
            openFileModal.Filter = "*.bsp";
            openFileModal.IsFilterChangeable = false;
            openFileModal.OnOpenedResource += LoadWorld;

            editor.AddInfoSection("Statistics", HandleStatisticsContent);
            editor.AddInfoSection("Materials", HandleMaterialsContent);
            editor.AddInfoSection("BSP collision", HandleBSPCollisionContent);

            worldTransform = new UniformBuffer<Matrix4x4>(diContainer.GetTag<GraphicsDevice>().ResourceFactory);
            worldTransform.Ref = Matrix4x4.Identity;
            AddDisposable(worldTransform);
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

            worldTransform.Ref = Matrix4x4.CreateTranslation(rwWorld.origin.ToNumerics());

            CurrentResource = resource;
            controls.ResetView();
            fbArea.IsDirty = true;
            Window.Title = $"World Viewer - {resource.Path.ToPOSIXString()}";
        }

        private void HandleRender(CommandList cl)
        {
            worldTransform.Update(cl);

            if (worldBuffers == null)
                return;
            worldBuffers.SetBuffers(cl);
            foreach (var subMesh in worldBuffers.SubMeshes)
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

        private void HandleMenuOpen()
        {
            openFileModal.InitialSelectedResource = CurrentResource;
            openFileModal.Modal.Open();
        }

        private void HandleStatisticsContent()
        {
            // TODO: Add WorldViewer statistics info section
        }

        private void HandleMaterialsContent()
        {
            // TODO: Add WorldViewer materials info section
        }

        private void HandleBSPCollisionContent()
        {
            // TODO: Add WorldViewer BSP collision info section
        }
    }
}
