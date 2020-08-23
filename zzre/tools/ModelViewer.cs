using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Veldrid;
using zzre.imgui;
using zzio.rwbs;
using zzre.core;
using System.Numerics;
using ImGuiNET;
using zzio.vfs;
using zzre.rendering;
using zzre.materials;

namespace zzre.tools
{
    public class ModelViewer : ListDisposable, IDocumentEditor
    {
        private readonly ITagContainer diContainer;
        private readonly TwoColumnEditorTag editor;
        private readonly OrbitControlsTag controls;
        private readonly ImGuiRenderer imGuiRenderer;
        private readonly GraphicsDevice device;
        private readonly FramebufferArea fbArea;
        private readonly TextureLoader textureLoader;
        private readonly IResourcePool resourcePool;
        private readonly DebugGridRenderer gridRenderer;
        private readonly OpenFileModal openFileModal;
        private readonly ModelMaterialEdit modelMaterialEdit;
        private readonly LocationBuffer locationBuffer;

        private RWGeometryBuffers? geometryBuffers;
        private ModelStandardMaterial[] materials = new ModelStandardMaterial[0];
        private DebugSkeletonRenderer? skeletonRenderer;

        public Window Window { get; }
        public IResource? CurrentResource { get; private set; }

        public ModelViewer(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            device = diContainer.GetTag<GraphicsDevice>();
            resourcePool = diContainer.GetTag<IResourcePool>();
            textureLoader = diContainer.GetTag<TextureLoader>();
            Window = diContainer.GetTag<WindowContainer>().NewWindow("Model Viewer");
            Window.InitialBounds = new Rect(float.NaN, float.NaN, 1100.0f, 600.0f);
            Window.AddTag(this);
            editor = new TwoColumnEditorTag(Window, diContainer);
            controls = new OrbitControlsTag(Window, diContainer);
            var onceAction = new OnceAction();
            Window.AddTag(onceAction);
            Window.OnContent += onceAction.Invoke;
            var menuBar = new MenuBarWindowTag(Window);
            menuBar.AddItem("Open", HandleMenuOpen);
            fbArea = Window.GetTag<FramebufferArea>();
            fbArea.OnRender += HandleRender;
            modelMaterialEdit = new ModelMaterialEdit(Window, diContainer);
            diContainer.GetTag<OpenDocumentSet>().AddEditor(this);

            openFileModal = new OpenFileModal(diContainer);
            openFileModal.Filter = "*.dff";
            openFileModal.IsFilterChangeable = false;
            openFileModal.OnOpenedResource += Load;
            imGuiRenderer = Window.Container.ImGuiRenderer;

            locationBuffer = new LocationBuffer(device);
            AddDisposable(locationBuffer);
            gridRenderer = new DebugGridRenderer(diContainer);
            gridRenderer.Material.LinkTransformsTo(controls.Projection, controls.View, controls.World);
            AddDisposable(gridRenderer);

            editor.AddInfoSection("Statistics", HandleStatisticsContent);
            editor.AddInfoSection("Materials", HandleMaterialsContent);
            editor.AddInfoSection("Skeleton", HandleSkeletonContent);
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
            var texturePath = textureLoader.GetTexturePathFromModel(resource.Path);

            geometryBuffers = new RWGeometryBuffers(diContainer, resource);
            AddDisposable(geometryBuffers);

            foreach (var oldTexture in materials.Select(m => m.MainTexture.Texture))
                imGuiRenderer.RemoveImGuiBinding(oldTexture);

            materials = new ModelStandardMaterial[geometryBuffers.SubMeshes.Count];
            foreach (var (rwMaterial, index) in geometryBuffers.SubMeshes.Select(s => s.Material).Indexed())
            {
                var material = materials[index] = new ModelStandardMaterial(diContainer);
                (material.MainTexture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(texturePath, rwMaterial);
                material.LinkTransformsTo(gridRenderer.Material);
                material.Uniforms.Ref = ModelStandardMaterialUniforms.Default;
                material.Uniforms.Ref.vertexColorFactor = 0.0f; // they seem to be set to some gray for models?
                material.Uniforms.Ref.tint = rwMaterial.color.ToFColor();
                AddDisposable(material);
            }
            modelMaterialEdit.Materials = materials;

            skeletonRenderer = null;
            if (geometryBuffers.Skin != null)
            {
                var skeleton = new Skeleton(geometryBuffers.Skin);
                skeletonRenderer = new DebugSkeletonRenderer(diContainer.ExtendedWith(gridRenderer.Material as IStandardTransformMaterial, locationBuffer), geometryBuffers, skeleton);
                AddDisposable(skeletonRenderer);
            }

            controls.ResetView();
            fbArea.IsDirty = true;
            CurrentResource = resource;
            Window.Title = $"Model Viewer - {resource.Path.ToPOSIXString()}";
        }

        private void HandleRender(CommandList cl)
        {
            locationBuffer.Update(cl);
            gridRenderer.Render(cl);
            if (geometryBuffers == null)
                return;            

            geometryBuffers.SetBuffers(cl);
            foreach (var (subMesh, index) in geometryBuffers.SubMeshes.Indexed())
            {
                (materials[index] as IMaterial).Apply(cl);
                cl.DrawIndexed(
                    indexStart: (uint)subMesh.IndexOffset,
                    indexCount: (uint)subMesh.IndexCount,
                    instanceCount: 1,
                    vertexOffset: 0,
                    instanceStart: 0);
            }

            skeletonRenderer?.Render(cl);
        }

        private void HandleStatisticsContent()
        {
            ImGui.Text($"Vertices: {geometryBuffers?.VertexCount}");
            ImGui.Text($"Triangles: {geometryBuffers?.TriangleCount}");
            ImGui.Text($"Submeshes: {geometryBuffers?.SubMeshes.Count}");
            ImGui.Text($"Bones: {skeletonRenderer?.Skeleton.Bones.Count}");
        }

        private void HandleMaterialsContent()
        {
            if (geometryBuffers == null)
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
    }
}
