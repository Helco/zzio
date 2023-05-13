using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using ImGuiNET;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;
using zzio.rwbs;

namespace zzre.tools;

public partial class SceneEditor
{
    private sealed class FOModel : BaseDisposable, ISelectable
    {
        private readonly ITagContainer diContainer;
        private readonly DeviceBufferRange locationRange;
        private readonly ClumpMesh mesh;
        private readonly AssetHandle<ClumpAsset> meshHandle;
        private readonly ModelMaterial[] materials;
        private readonly List<AssetHandle> materialHandles = [];

        public Location Location { get; } = new Location();
        public zzio.scn.FOModel SceneFOModel { get; }

        public string Title => $"#{SceneFOModel.idx} - {SceneFOModel.filename}";
        public IRaycastable SelectableBounds => mesh.BoundingBox.TransformToWorld(Location);
        public IRaycastable RenderedBounds => SelectableBounds;
        public float ViewSize => mesh.BoundingBox.MaxSizeComponent;

        public void SyncWithScene()
        {
            SceneFOModel.pos = Location.LocalPosition;
        }

        public FOModel(ITagContainer diContainer, zzio.scn.FOModel sceneModel)
        {
            this.diContainer = diContainer;
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

            var assetRegistry = diContainer.GetTag<IAssetRegistry>();
            meshHandle = assetRegistry.Load(ClumpAsset.Info.Model(sceneModel.filename), AssetLoadPriority.Synchronous).As<ClumpAsset>();
            mesh = meshHandle.Get().Mesh;
            if (mesh.IsEmpty)
            {
                materials = [];
                return;
            }
            materials = mesh.Materials.Select(rwMaterial =>
            {

                var material = new ModelMaterial(diContainer);
                var rwTexture = (RWTexture)rwMaterial.FindChildById(SectionId.Texture, true)!;
                var rwTextureName = (RWString)rwTexture.FindChildById(SectionId.String, true)!;
                var textureHandle = assetRegistry.LoadTexture(textureBasePaths, rwTextureName.value, AssetLoadPriority.Synchronous, material);
                var samplerHandle = assetRegistry.LoadSampler(SamplerDescription.Linear);
                materialHandles.Add(textureHandle);
                materialHandles.Add(samplerHandle);
                material.Texture.Texture = textureHandle.Get().Texture;
                material.Sampler.Sampler = samplerHandle.Get().Sampler;
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
            meshHandle.Dispose();
            foreach (var material in materials)
                material.Dispose();
            foreach (var handle in materialHandles)
                handle.Dispose();
        }

        public void Render(CommandList cl)
        {
            if (mesh.IsEmpty)
                return;
            materials.First().ApplyAttributes(cl, mesh);
            cl.SetIndexBuffer(mesh.IndexBuffer, mesh.IndexFormat);
            for (int i = 0; i < mesh.SubMeshes.Count; i++)
            {
                var subMesh = mesh.SubMeshes[i];
                (materials[i] as IMaterial).Apply(cl);
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
            if (Hyperlink("Model", SceneFOModel.filename))
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

    private sealed class FOModelComponent : BaseDisposable, IEnumerable<ISelectable>
    {
        private static readonly IReadOnlyList<string> DetailLabels =
        [
            "Invisible",
            "Detail Level 0",
            "Detail Level 1",
            "Detail Level 2",
            "Detail Level 3"
        ];
        private readonly ITagContainer diContainer;
        private readonly SceneEditor editor;

        private FOModel[] models = [];
        private int detailLevel = 4; // Detail levels from 1, invisible is 0

        public FOModelComponent(ITagContainer diContainer)
        {
            diContainer.AddTag(this);
            this.diContainer = diContainer;
            editor = diContainer.GetTag<SceneEditor>();
            editor.selectableContainers.Add(this);
            editor.fbArea.OnRender += HandleRender;
            editor.OnLoadScene += HandleLoadScene;
            diContainer.GetTag<MenuBarWindowTag>().AddRadio("View/FOModels", DetailLabels, () => ref detailLevel, () => editor.fbArea.IsDirty = true);
            editor.editor.AddInfoSection("FOModels", HandleInfoSection, false);
        }

        public void SyncWithScene()
        {
            foreach (var foModel in models)
                foModel.SyncWithScene();
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
            for (int i = 0; i < models.Length; i++)
            {
                var model = models[i];
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
                PushID(i);
                model.Content();
                PopID();
                TreePop();
            }
        }
        private FOModel? FindCurrentFoModel()
        {
            foreach (var foModel in models)
            {
                if (foModel == editor.Selected)
                    return foModel;
            }
            return null;
        }
        private uint GetNextAvailableFoModelID()
        {
            uint result = 1;
            foreach (var foModel in models)
            {
                result = Math.Max(result, foModel.SceneFOModel.idx);
            }
            return result + 1;
        }
        public void DeleteCurrentFoModel()
        {
            var currentFoModel = FindCurrentFoModel();
            if (currentFoModel == null || editor.scene == null)
                return;

            SyncWithScene();


            editor.scene.foModels = editor.scene.foModels.Where(
                model => model.idx != currentFoModel.SceneFOModel.idx
            ).ToArray();

            HandleLoadScene();
            editor.Selected = null;
        }
        public void DuplicateCurrentFoModel()
        {
            var currentFoModel = FindCurrentFoModel();
            if (currentFoModel == null || editor.scene == null)
                return;

            SyncWithScene();

            var copy = currentFoModel.SceneFOModel.Clone();
            copy.idx = GetNextAvailableFoModelID();
            editor.scene.foModels = editor.scene.foModels.Append(copy).ToArray();
            HandleLoadScene();
            editor.Selected = models.Last();
        }
        IEnumerator<ISelectable> IEnumerable<ISelectable>.GetEnumerator() => ((IEnumerable<ISelectable>)models).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => models.Cast<ISelectable>().GetEnumerator();
    }
}
