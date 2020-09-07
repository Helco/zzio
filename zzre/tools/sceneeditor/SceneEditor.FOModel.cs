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

namespace zzre.tools
{
    public partial class SceneEditor
    {
        private class FOModel : BaseDisposable, ISelectable
        {
            private readonly ITagContainer diContainer;
            private readonly DeviceBufferRange locationRange;
            private readonly ClumpBuffers clumpBuffers;
            private readonly ModelStandardMaterial[] materials;

            public Location Location { get; } = new Location();
            public zzio.scn.FOModel SceneFOModel { get; }

            public string Title => $"#{SceneFOModel.idx} - {SceneFOModel.filename}";
            public Box Bounds => clumpBuffers.Bounds;

            public FOModel(ITagContainer diContainer, zzio.scn.FOModel sceneModel)
            {
                this.diContainer = diContainer;
                var textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
                var textureBasePaths = new[]
                {
                    new FilePath("resources/textures/models"),
                    new FilePath("resources/textures/worlds")
                };
                var camera = diContainer.GetTag<Camera>();
                SceneFOModel = sceneModel;
                locationRange = diContainer.GetTag<LocationBuffer>().Add(Location);
                Location.LocalPosition = sceneModel.pos.ToNumerics();
                Location.LocalRotation = sceneModel.rot.ToNumericsRotation();
                //Location.LocalScale = sceneModel.scale.ToNumerics(); // TODO: SceneModel scale seems bugged for some models (e.g. z == 0)

                clumpBuffers = new ClumpBuffers(diContainer, new FilePath("resources/models/models").Combine(sceneModel.filename + ".dff"));
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
                if (ImGuiEx.Hyperlink("Model", SceneFOModel.filename))
                {
                    var fullPath = new FilePath("resources/models/models").Combine(SceneFOModel.filename + ".dff");
                    diContainer.GetTag<OpenDocumentSet>().OpenWith<ModelViewer>(fullPath);
                }
                var color = SceneFOModel.color.ToFColor().ToNumerics();
                ColorEdit4("Color", ref color, ImGuiColorEditFlags.NoPicker);
                NewLine();
                
                var pos = Location.LocalPosition;
                var rotEuler = Location.LocalRotation.ToEuler() * 180.0f / MathF.PI;
                var scale = Location.LocalScale;
                hasChanged |= DragFloat3("Position", ref pos);
                hasChanged |= DragFloat3("Rotation", ref rotEuler);
                hasChanged |= DragFloat3("Scale", ref scale);
                NewLine();

                var f1 = SceneFOModel.f1;
                var f2 = SceneFOModel.f2;
                var f3 = SceneFOModel.f3;
                var f4 = SceneFOModel.f4;
                var f5 = SceneFOModel.f5;
                var ff2 = (int)SceneFOModel.ff2;
                var ff3 = (int)SceneFOModel.ff3;
                var i7 = SceneFOModel.i7;
                var renderType = SceneFOModel.renderType;
                var worldDetailLevel = (int)SceneFOModel.worldDetailLevel;
                InputFloat("F1", ref f1);
                InputFloat("F2", ref f2);
                InputFloat("F3", ref f3);
                InputFloat("F4", ref f4);
                InputFloat("F5", ref f5);
                InputInt("FF2", ref ff2);
                InputInt("FF3", ref ff3);
                InputInt("I7", ref i7);
                SliderInt("Detail Level", ref worldDetailLevel, 0, 3);
                ImGuiEx.EnumCombo("Render Type", ref renderType);

                if (hasChanged)
                {
                    rotEuler = (rotEuler * MathF.PI / 180.0f) - Location.LocalRotation.ToEuler();
                    Location.LocalPosition = pos;
                    Location.LocalRotation *= Quaternion.CreateFromYawPitchRoll(rotEuler.Y, rotEuler.X, rotEuler.Z);
                    Location.LocalScale = scale;
                    diContainer.GetTag<FramebufferArea>().IsDirty = true;
                }
            }
        }
        
        private class FOModelComponent : BaseDisposable
        {
            private readonly ITagContainer diContainer;
            private readonly SceneEditor editor;

            private FOModel[] models = new FOModel[0];
            private int detailLevel = 4; // Detail levels from 1, invisible is 0

            public FOModelComponent(ITagContainer diContainer)
            {
                diContainer.AddTag(this);
                this.diContainer = diContainer;
                editor = diContainer.GetTag<SceneEditor>();
                editor.fbArea.OnRender += HandleRender;
                editor.OnLoadScene += HandleLoadScene;
                diContainer.GetTag<MenuBarWindowTag>().AddRadio("View/FOModels", new[]
                {
                    "Invisible",
                    "Detail Level 0",
                    "Detail Level 1",
                    "Detail Level 2",
                    "Detail Level 3"
                }, () => ref detailLevel, () => editor.fbArea.IsDirty = true);
                editor.editor.AddInfoSection("FOModels", HandleInfoSection, false);
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
                models = new FOModel[0];
                if (editor.scene == null)
                    return;

                models = editor.scene.foModels.Select(m => new FOModel(diContainer, m)).ToArray();
            }

            private void HandleRender(CommandList cl)
            {
                if (detailLevel == 0)
                    return;
                foreach (var model in models)
                {
                    if (model.SceneFOModel.worldDetailLevel <= detailLevel - 1)
                        model.Render(cl);
                }
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
