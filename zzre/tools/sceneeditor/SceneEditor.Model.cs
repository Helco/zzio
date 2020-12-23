using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Veldrid;
using zzio.scn;
using zzio.utils;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using ImGuiNET;
using static ImGuiNET.ImGui;
using System.Security.Cryptography.X509Certificates;

namespace zzre.tools
{
    public partial class SceneEditor
    {
        private class Model : BaseDisposable, ISelectable
        {
            private readonly ITagContainer diContainer;
            private readonly DeviceBufferRange locationRange;
            private readonly ClumpBuffers clumpBuffers;
            private readonly ModelStandardMaterial[] materials;

            public Location Location { get; } = new Location();
            public zzio.scn.Model SceneModel { get; }
            public zzio.scn.Behavior? SceneBehaviour { get; }

            public string Title => $"#{SceneModel.idx} - {SceneModel.filename}";
            public Box Bounds => clumpBuffers.Bounds;

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
                Location.LocalPosition = sceneModel.pos.ToNumerics();
                Location.LocalRotation = sceneModel.rot.ToNumericsRotation();
                //Location.LocalScale = sceneModel.scale.ToNumerics(); // TODO: SceneModel scale seems bugged for some models (e.g. z == 0)

                var clumpLoader = diContainer.GetTag<IAssetLoader<ClumpBuffers>>();
                clumpBuffers = clumpLoader.Load(new FilePath("resources/models/models").Combine(sceneModel.filename + ".dff"));
                materials = clumpBuffers.SubMeshes.Select(subMesh =>
                {
                    var rwMaterial = subMesh.Material;
                    var material = new ModelStandardMaterial(diContainer);
                    (material.MainTexture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(textureBasePaths, rwMaterial);
                    material.LinkTransformsTo(camera);
                    material.World.BufferRange = locationRange;
                    material.Uniforms.Ref = ModelStandardMaterialUniforms.Default;
                    material.Uniforms.Ref.vertexColorFactor = 0.0f;
                    material.Uniforms.Ref.tint = rwMaterial.color.ToFColor() * sceneModel.color;
                    return material;
                }).ToArray();
            }

            protected override void DisposeManaged()
            {
                base.DisposeManaged();
                diContainer.GetTag<LocationBuffer>().Remove(locationRange);
                clumpBuffers.Dispose();
                foreach (var material in materials)
                    material.Dispose();
            }

            public void Render(CommandList cl)
            {
                clumpBuffers.SetBuffers(cl);
                foreach (var (subMesh, index) in clumpBuffers.SubMeshes.Indexed())
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
                if (ImGuiEx.Hyperlink("Model", SceneModel.filename))
                {
                    var fullPath = new FilePath("resources/models/models").Combine(SceneModel.filename + ".dff");
                    diContainer.GetTag<OpenDocumentSet>().OpenWith<ModelViewer>(fullPath);
                }
                var color = SceneModel.color.ToFColor().ToNumerics();
                ColorEdit4("Color", ref color, ImGuiColorEditFlags.NoPicker);
                NewLine();
                
                var pos = Location.LocalPosition;
                var rotEuler = Location.LocalRotation.ToEuler() * 180.0f / MathF.PI;
                hasChanged |= DragFloat3("Position", ref pos);
                hasChanged |= DragFloat3("Rotation", ref rotEuler);
                NewLine();

                int i1 = SceneModel.i1;
                int i2 = SceneModel.i2;
                int i15 = SceneModel.i15;
                InputInt("I1", ref i1);
                InputInt("I2", ref i2);
                InputInt("I15", ref i15);

                NewLine();
                var behaviorType = SceneBehaviour?.type ?? BehaviourType.Unknown;
                ImGuiEx.EnumCombo("Behavior", ref behaviorType);

                if (hasChanged)
                {
                    rotEuler = (rotEuler * MathF.PI / 180.0f) - Location.LocalRotation.ToEuler();
                    Location.LocalPosition = pos;
                    Location.LocalRotation *= Quaternion.CreateFromYawPitchRoll(rotEuler.Y, rotEuler.X, rotEuler.Z);
                    diContainer.GetTag<FramebufferArea>().IsDirty = true;
                }
            }
        }
        
        private class ModelComponent : BaseDisposable
        {
            private readonly ITagContainer diContainer;
            private readonly SceneEditor editor;

            private Model[] models = new Model[0];
            private bool isVisible = true;

            public ModelComponent(ITagContainer diContainer)
            {
                diContainer.AddTag(this);
                this.diContainer = diContainer;
                editor = diContainer.GetTag<SceneEditor>();
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
                models = new Model[0];
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
        }
    }
}
