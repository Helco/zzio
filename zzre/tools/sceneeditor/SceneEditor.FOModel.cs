using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using ImGuiNET;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;
using System.Collections;
using zzio;

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
            public IRaycastable SelectableBounds => clumpBuffers.Bounds.TransformToWorld(Location);
            public IRaycastable RenderedBounds => SelectableBounds;
            public float ViewSize => clumpBuffers.Bounds.MaxSizeComponent;

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
                Location.LocalPosition = sceneModel.pos;
                Location.LocalRotation = sceneModel.rot.ToZZRotation();

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
                var color = SceneFOModel.color.ToFColor();
                ColorEdit4("Color", ref color, ImGuiColorEditFlags.NoPicker);
                NewLine();

                var pos = Location.LocalPosition;
                var rotEuler = Location.LocalRotation.ToEuler() * 180.0f / MathF.PI;
                hasChanged |= DragFloat3("Position", ref pos);
                hasChanged |= DragFloat3("Rotation", ref rotEuler);
                NewLine();


                DragFloatRange2("FadeOut", ref SceneFOModel.fadeOutMin, ref SceneFOModel.fadeOutMax);
                SliderFloat("Ambient", ref SceneFOModel.surfaceProps.ambient, -1f, 1f);
                SliderFloat("Specular", ref SceneFOModel.surfaceProps.specular, -1f, 1f);
                SliderFloat("Diffuse", ref SceneFOModel.surfaceProps.diffuse, -1f, 1f);
                Checkbox("Use cached model", ref SceneFOModel.useCachedModels);
                SliderInt("Wiggle Speed", ref SceneFOModel.wiggleAmpl, 0, 4);
                SliderInt("Detail Level", ref SceneFOModel.worldDetailLevel, 0, 3);
                EnumCombo("Render Type", ref SceneFOModel.renderType);
                InputInt("Unused", ref SceneFOModel.unused);

                if (hasChanged)
                {
                    rotEuler = (rotEuler * MathF.PI / 180.0f) - Location.LocalRotation.ToEuler();
                    Location.LocalPosition = pos;
                    Location.LocalRotation *= Quaternion.CreateFromYawPitchRoll(rotEuler.Y, rotEuler.X, rotEuler.Z);
                    diContainer.GetTag<FramebufferArea>().IsDirty = true;
                }
            }
        }

        private class FOModelComponent : BaseDisposable, IEnumerable<ISelectable>
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
                editor.selectableContainers.Add(this);
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
                foreach (var model in models.Where(model => model.SceneFOModel.worldDetailLevel <= detailLevel - 1))
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
}
