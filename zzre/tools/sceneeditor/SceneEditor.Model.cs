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

namespace zzre.tools
{
    public partial class SceneEditor
    {
        private class Model : BaseDisposable
        {
            private readonly ITagContainer diContainer;
            private readonly DeviceBufferRange locationRange;
            private readonly ClumpBuffers clumpBuffers;
            private readonly IMaterial[] materials;

            public Location Location { get; } = new Location();
            public zzio.scn.Model SceneModel { get; }

            public Model(ITagContainer diContainer, zzio.scn.Model sceneModel)
            {
                this.diContainer = diContainer;
                var textureLoader = diContainer.GetTag<TextureLoader>();
                var textureBasePaths = new[]
                {
                    new FilePath("resources/textures/models"),
                    new FilePath("resources/textures/worlds")
                };
                var parentMaterial = diContainer.GetTag<IStandardTransformMaterial>();
                SceneModel = sceneModel;
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
                    material.LinkTransformsTo(parentMaterial);
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
                    materials[index].Apply(cl);
                    cl.DrawIndexed(
                        indexStart: (uint)subMesh.IndexOffset,
                        indexCount: (uint)subMesh.IndexCount,
                        instanceCount: 1,
                        instanceStart: 0,
                        vertexOffset: 0);
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
                editor.Window.GetTag<MenuBarWindowTag>().AddCheckbox("View/Models", () => ref isVisible, () => editor.fbArea.IsDirty = true);
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

                models = editor.scene.models.Select(m => new Model(diContainer, m)).ToArray();
            }

            private void HandleRender(CommandList cl)
            {
                if (!isVisible)
                    return;
                foreach (var model in models)
                    model.Render(cl);
            }
        }
    }
}
