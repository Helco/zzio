using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.scn;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using ImGuiNET;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;

namespace zzre.tools;

public partial class SceneEditor
{
    private sealed class Model : BaseDisposable, ISelectable
    {
        private readonly ITagContainer diContainer;
        private readonly DeviceBufferRange locationRange;
        private readonly ClumpMesh mesh;
        private readonly ModelMaterial[] materials;

        public Location Location { get; } = new Location();
        public zzio.scn.Model SceneModel { get; }
        public zzio.scn.Behavior? SceneBehaviour { get; }

        public string Title => $"#{SceneModel.idx} - {SceneModel.filename}";
        public IRaycastable SelectableBounds => mesh.BoundingBox.TransformToWorld(Location);
        public IRaycastable RenderedBounds => SelectableBounds;
        public float ViewSize => mesh.BoundingBox.MaxSizeComponent;

        public Model(ITagContainer diContainer, zzio.scn.Model sceneModel, Behavior? sceneBehavior)
        {
            this.diContainer = diContainer;
            var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
            var textureBasePaths = new[]
            {
                new FilePath("resources/textures/models"),
                new FilePath("resources/textures/worlds")
            };
            var camera = diContainer.GetTag<Camera>();
            SceneModel = sceneModel;
            SceneBehaviour = sceneBehavior;
            locationRange = diContainer.GetTag<LocationBuffer>().Add(Location);
            Location.LocalPosition = sceneModel.pos;
            Location.LocalRotation = sceneModel.rot.ToZZRotation();

            var clumpLoader = diContainer.GetTag<IAssetLoader<ClumpMesh>>();
            mesh = clumpLoader.Load(new FilePath("resources/models/models").Combine(sceneModel.filename + ".dff"));
            if (mesh.IsEmpty)
            {
                materials = [];
                return;
            }
            materials = mesh.Materials.Select(rwMaterial =>
            {
                var material = new ModelMaterial(diContainer);
                (material.Texture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(textureBasePaths, rwMaterial);
                material.LinkTransformsTo(camera);
                material.World.BufferRange = locationRange;
                material.Factors.Ref = ModelFactors.Default;
                material.Factors.Ref.vertexColorFactor = 0.0f;
                material.Tint.Ref = rwMaterial.color.ToFColor() * sceneModel.color;
                return material;
            }).ToArray();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            diContainer.GetTag<LocationBuffer>().Remove(locationRange);
            mesh.Dispose();
            foreach (var material in materials)
                material.Dispose();
        }

        public void Render(CommandList cl)
        {
            if (mesh.IsEmpty)
                return;
            materials.First().ApplyAttributes(cl, mesh);
            cl.SetIndexBuffer(mesh.IndexBuffer, mesh.IndexFormat);
            foreach (var (subMesh, index) in mesh.SubMeshes.Indexed())
            {
                (materials[index] as IMaterial).Apply(cl);
                cl.DrawIndexed(
                    indexStart: (uint)subMesh.IndexOffset,
                    indexCount: (uint)subMesh.IndexCount,
                    instanceCount: 1,
                    instanceStart: 0,
                    vertexOffset: 0);
            }
        }

        public void Content()
        {
            bool hasChanged = false;
            if (Hyperlink("Model", SceneModel.filename))
            {
                var fullPath = new FilePath("resources/models/models").Combine(SceneModel.filename + ".dff");
                diContainer.GetTag<OpenDocumentSet>().OpenWith<ModelViewer>(fullPath);
            }
            var color = SceneModel.color.ToFColor();
            ColorEdit4("Color", ref color, ImGuiColorEditFlags.NoPicker);
            NewLine();

            var pos = Location.LocalPosition;
            var rotEuler = Location.LocalRotation.ToEuler() * 180.0f / MathF.PI;
            hasChanged |= DragFloat3("Position", ref pos);
            hasChanged |= DragFloat3("Rotation", ref rotEuler);
            NewLine();

            SliderFloat("Ambient", ref SceneModel.surfaceProps.ambient, -1f, 1f);
            SliderFloat("Specular", ref SceneModel.surfaceProps.specular, -1f, 1f);
            SliderFloat("Diffuse", ref SceneModel.surfaceProps.diffuse, -1f, 1f);
            Checkbox("Use cached models", ref SceneModel.useCachedModels);
            SliderInt("Wiggle Speed", ref SceneModel.wiggleAmpl, 0, 4);
            Checkbox("Is only visual", ref SceneModel.isVisualOnly);

            NewLine();
            var behaviorType = SceneBehaviour?.type ?? BehaviourType.Unknown;
            EnumCombo("Behavior", ref behaviorType);

            if (hasChanged)
            {
                rotEuler = (rotEuler * MathF.PI / 180.0f) - Location.LocalRotation.ToEuler();
                Location.LocalPosition = pos;
                Location.LocalRotation *= Quaternion.CreateFromYawPitchRoll(rotEuler.Y, rotEuler.X, rotEuler.Z);
                diContainer.GetTag<FramebufferArea>().IsDirty = true;
            }
        }
    }

    private sealed class ModelComponent : BaseDisposable, IEnumerable<ISelectable>
    {
        private readonly ITagContainer diContainer;
        private readonly SceneEditor editor;

        private Model[] models = [];
        private bool isVisible = true;

        public ModelComponent(ITagContainer diContainer)
        {
            diContainer.AddTag(this);
            this.diContainer = diContainer;
            editor = diContainer.GetTag<SceneEditor>();
            editor.selectableContainers.Add(this);
            editor.fbArea.OnRender += HandleRender;
            editor.OnLoadScene += HandleLoadScene;
            diContainer.GetTag<MenuBarWindowTag>().AddCheckbox("View/Models", () => ref isVisible, () => editor.fbArea.IsDirty = true);
            editor.editor.AddInfoSection("Models", HandleInfoSection, false);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            foreach (var model in models)
                model.Dispose();
        }

        private void HandleLoadScene()
        {
            foreach (var oldModel in models)
                oldModel.Dispose();
            models = [];
            if (editor.scene == null)
                return;

            models = editor.scene.models.Select(m => new Model(diContainer, m, editor.scene.behaviors.FirstOrDefault(b => b.modelId == m.idx))).ToArray();
        }

        private void HandleRender(CommandList cl)
        {
            if (!isVisible)
                return;
            foreach (var model in models)
                model.Render(cl);
        }

        private void HandleInfoSection()
        {
            foreach (var (model, index) in models.Indexed())
            {
                var flags =
                    ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick |
                    (model == editor.Selected ? ImGuiTreeNodeFlags.Selected : 0);
                var isOpen = TreeNodeEx(model.Title, flags);
                if (IsItemClicked())
                    editor.Selected = model;
                if (IsItemClicked() && IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    editor.MoveCameraToSelected();
                if (!isOpen)
                    continue;
                PushID(index);
                model.Content();
                PopID();
                TreePop();
            }
        }

        IEnumerator<ISelectable> IEnumerable<ISelectable>.GetEnumerator() => ((IEnumerable<ISelectable>)models).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => models.Cast<ISelectable>().GetEnumerator();
    }
}
