using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Veldrid;
using zzre.imgui;
using zzio.rwbs;
using zzre.core;
using zzio.utils;
using System.Numerics;
using ImGuiNET;
using zzio.vfs;
using zzre.rendering;
using zzre.materials;
using zzio.primitives;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp;

namespace zzre.tools
{
    public class ModelViewer : ListDisposable
    {
        private const float TexturePreviewSize = 5.0f;
        private const float TextureHoverSizeFactor = 0.4f;

        private readonly ITagContainer diContainer;
        private readonly SimpleEditorTag editor;
        private readonly ImGuiRenderer imGuiRenderer;
        private readonly GraphicsDevice device;
        private readonly FramebufferArea fbArea;
        private readonly IResourcePool resourcePool;
        private readonly DebugGridRenderer gridRenderer;
        private readonly OpenFileModal openFileModal;

        private RWGeometryBuffers? geometryBuffers;
        private ModelStandardMaterial[] materials = new ModelStandardMaterial[0];
        private IntPtr[] textureBindings = new IntPtr[0];
        private DebugSkeletonRenderer? skeletonRenderer;

        public Window Window { get; }
        public IResource? CurrentResource { get; private set; }

        public ModelViewer(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            device = diContainer.GetTag<GraphicsDevice>();
            resourcePool = diContainer.GetTag<IResourcePool>();
            Window = diContainer.GetTag<WindowContainer>().NewWindow("Model Viewer");
            Window.AddTag(this);
            editor = new SimpleEditorTag(Window, diContainer);
            new DeferredCallerTag(Window);
            var menuBar = new MenuBarWindowTag(Window);
            menuBar.AddItem("Open", HandleMenuOpen);
            fbArea = Window.GetTag<FramebufferArea>();
            fbArea.OnRender += HandleRender;
            AddDisposable(fbArea);

            openFileModal = new OpenFileModal(diContainer);
            openFileModal.Filter = "*.dff";
            openFileModal.IsFilterChangeable = false;
            openFileModal.OnOpenedResource += LoadModel;
            imGuiRenderer = Window.Container.ImGuiRenderer;

            gridRenderer = new DebugGridRenderer(diContainer);
            gridRenderer.Material.Transformation.Buffer = editor.Transform.Buffer;
            AddDisposable(gridRenderer);

            editor.AddInfoSection("Statistics", HandleStatisticsContent);
            editor.AddInfoSection("Materials", HandleMaterialsContent);
            editor.AddInfoSection("Skeleton", HandleSkeleton);
        }

        public void LoadModel(string pathText)
        {
            var resource = resourcePool.FindFile(pathText);
            if (resource == null)
                throw new FileNotFoundException($"Could not find model at {pathText}");
            LoadModel(resource);
        }

        public void LoadModel(IResource resource) => 
            Window.GetTag<DeferredCallerTag>().Next += () => LoadModelNow(resource);

        private void LoadModelNow(IResource resource)
        {
            if (resource.Equals(CurrentResource))
                return;
            CurrentResource = null;
            var texturePath = GetTexturePathFromModel(resource.Path);

            using var contentStream = resource.OpenContent();
            if (contentStream == null)
                throw new IOException($"Could not open model at {resource.Path.ToPOSIXString()}");
            var clump = Section.ReadNew(contentStream);
            if (clump.sectionId != SectionId.Clump)
                throw new InvalidDataException($"Expected a root clump section, got a {clump.sectionId}");

            geometryBuffers = new RWGeometryBuffers(diContainer, (RWClump)clump);
            AddDisposable(geometryBuffers);

            foreach (var oldTexture in materials.Select(m => m.MainTexture.Texture))
                imGuiRenderer.RemoveImGuiBinding(oldTexture);

            materials = new ModelStandardMaterial[geometryBuffers.SubMeshes.Count];
            textureBindings = new IntPtr[materials.Length];
            foreach (var (rwMaterial, index) in geometryBuffers.SubMeshes.Select(s => s.Material).Indexed())
            {
                var material = materials[index] = new ModelStandardMaterial(diContainer);
                (material.MainTexture.Texture, material.Sampler.Sampler) = LoadTextureFromMaterial(texturePath, rwMaterial);
                material.Transformation.Buffer = editor.Transform.Buffer;
                material.Uniforms.Ref = ModelStandardMaterialUniforms.Default;
                material.Uniforms.Ref.vertexColorFactor = 0.0f; // they seem to be set to some gray for models?
                material.Uniforms.Ref.tint = rwMaterial.color.ToFColor();
                AddDisposable(material);

                textureBindings[index] = imGuiRenderer.GetOrCreateImGuiBinding(device.ResourceFactory, material.MainTexture.Texture);
            }

            var skin = clump.FindChildById(SectionId.SkinPLG, true);
            skeletonRenderer = null;
            if (skin != null)
            {
                skeletonRenderer = new DebugSkeletonRenderer(diContainer, geometryBuffers, new Skeleton((RWSkinPLG)skin));
                skeletonRenderer.BoneMaterial.Transformation.Buffer = editor.Transform.Buffer;
                skeletonRenderer.SkinMaterial.Transformation.Buffer = editor.Transform.Buffer;
                skeletonRenderer.SkinHighlightedMaterial.Transformation.Buffer = editor.Transform.Buffer;
                AddDisposable(skeletonRenderer);
            }

            editor.ResetView();
            fbArea.IsDirty = true;
            CurrentResource = resource;
        }

        private FilePath GetTexturePathFromModel(FilePath modelPath)
        {
            var modelDirPartI = modelPath.Parts.IndexOf(p => p.ToLowerInvariant() == "models");
            var context = modelPath.Parts[modelDirPartI + 1];
            return new FilePath("resources/textures").Combine(context);
        }

        private (Texture, Sampler) LoadTextureFromMaterial(FilePath basePath, RWMaterial material)
        {
            var texSection = (RWTexture)material.FindChildById(SectionId.Texture, true);
            var addressModeU = ConvertAddressMode(texSection.uAddressingMode);
            var samplerDescription = new SamplerDescription()
            {
                AddressModeU = addressModeU,
                AddressModeV = ConvertAddressMode(texSection.vAddressingMode, addressModeU),
                Filter = ConvertFilterMode(texSection.filterMode)
            };

            var nameSection = (RWString)texSection.FindChildById(SectionId.String, true);
            using var textureStream = resourcePool.FindAndOpen(basePath.Combine(nameSection.value + ".bmp").ToPOSIXString());
            var texture = new Veldrid.ImageSharp.ImageSharpTexture(textureStream, false);
            var result = (
                texture.CreateDeviceTexture(device, device.ResourceFactory),
                device.ResourceFactory.CreateSampler(samplerDescription));
            result.Item1.Name = nameSection.value;
            AddDisposable(result.Item1);
            AddDisposable(result.Item2);
            return result;
        }

        private SamplerAddressMode ConvertAddressMode(TextureAddressingMode mode, SamplerAddressMode? altMode = null) => mode switch
        {
            TextureAddressingMode.Wrap => SamplerAddressMode.Wrap,
            TextureAddressingMode.Mirror => SamplerAddressMode.Mirror,
            TextureAddressingMode.Clamp => SamplerAddressMode.Clamp,
            TextureAddressingMode.Border => SamplerAddressMode.Border,

            TextureAddressingMode.NATextureAddress => altMode ?? throw new NotImplementedException(),
            TextureAddressingMode.Unknown => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };

        private SamplerFilter ConvertFilterMode(TextureFilterMode mode) => mode switch
        {
            TextureFilterMode.Nearest => SamplerFilter.MinPoint_MagPoint_MipPoint,
            TextureFilterMode.Linear => SamplerFilter.MinLinear_MagLinear_MipPoint,
            TextureFilterMode.MipNearest => SamplerFilter.MinPoint_MagPoint_MipPoint,
            TextureFilterMode.MipLinear => SamplerFilter.MinLinear_MagLinear_MipPoint,
            TextureFilterMode.LinearMipNearest => SamplerFilter.MinPoint_MagPoint_MipLinear,
            TextureFilterMode.LinearMipLinear => SamplerFilter.MinLinear_MagLinear_MipLinear,

            TextureFilterMode.NAFilterMode => throw new NotImplementedException(),
            TextureFilterMode.Unknown => throw new NotImplementedException(),
            _ => throw new NotImplementedException(),
        };

        private void HandleRender(CommandList cl)
        {
            if (geometryBuffers == null)
                return;
            gridRenderer.Render(cl);
            

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
            ImGui.Text($"Bones: {skeletonRenderer?.Skeleton.BoneCount}");
        }

        private void HandleMaterialsContent()
        {
            if (geometryBuffers == null)
                return;

            ImGui.Text("Globals");
            var mat = materials.First().Uniforms.Value;
            bool didChange = false;
            didChange |= ImGui.SliderFloat("Vertex Color Factor", ref mat.vertexColorFactor, 0.0f, 1.0f);
            didChange |= ImGui.SliderFloat("Global Tint Factor", ref mat.tintFactor, 0.0f, 1.0f);
            if (didChange)
            {
                foreach (var material in materials)
                {
                    material.Uniforms.Ref.vertexColorFactor = mat.vertexColorFactor;
                    material.Uniforms.Ref.tintFactor = mat.tintFactor;
                }
                fbArea.IsDirty = true;
            }

            ImGui.NewLine();
            ImGui.Text("Materials");
            foreach (var (rwMat, index) in geometryBuffers.SubMeshes.Select(s => s.Material).Indexed())
            {
                bool isVisible = materials[index].Uniforms.Value.alphaReference < 2f;
                ImGui.PushID(index);
                if (ImGui.SmallButton(isVisible ? "V" : "H"))
                {
                    isVisible = !isVisible;
                    materials[index].Uniforms.Ref.alphaReference = isVisible ? 0.03f : 2.0f;
                    fbArea.IsDirty = true;
                }
                ImGui.PopID();
                ImGui.SameLine();
                if (!ImGui.TreeNodeEx($"Material #{index}", ImGuiTreeNodeFlags.DefaultOpen))
                    continue;
                var color = rwMat.color.ToFColor().ToNumerics();
                ImGui.ColorEdit4("Color", ref color, ImGuiColorEditFlags.NoPicker | ImGuiColorEditFlags.Float);
                TexturePreview(materials[index].MainTexture.Texture, textureBindings[index]);

                ImGui.TreePop();
            }
        }

        private void TexturePreview(Texture? texture, IntPtr binding)
        {
            if (texture == null)
                return;
            ImGui.Columns(2, null, false);
            var previewTexSize = ImGui.GetTextLineHeight() * TexturePreviewSize;
            ImGui.SetColumnWidth(0, previewTexSize + ImGui.GetStyle().FramePadding.X * 3);
            ImGui.Image(binding, Vector2.One * previewTexSize);
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                var viewportSize = ImGui.GetWindowViewport().Size;
                var hoverTexSize = Math.Min(viewportSize.X, viewportSize.Y) * TextureHoverSizeFactor * Vector2.One;
                ImGui.Image(binding, hoverTexSize);
                ImGui.EndTooltip();
            }
            ImGui.NextColumn();
            ImGui.Text(texture?.Name);
            ImGui.Text($"{texture?.Width}x{texture?.Height}");
            ImGui.Text($"{texture?.Format}");
            ImGui.Columns(1);
        }

        private void HandleMenuOpen()
        {
            openFileModal.InitialSelectedResource = CurrentResource;
            openFileModal.Modal.Open();
        }

        private void HandleSkeleton()
        {
            if (skeletonRenderer == null)
            {
                ImGui.Text("This model has no skeleton.");
                return;
            }

            if (skeletonRenderer.Content())
                fbArea.IsDirty = true;
        }
    }
}
