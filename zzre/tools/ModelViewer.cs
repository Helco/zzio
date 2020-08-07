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

namespace zzre.tools
{
    public class ModelViewer : ListDisposable
    {
        private const float FieldOfView = 60.0f * 3.141592653f / 180.0f;

        private readonly ITagContainer diContainer;
        private readonly GraphicsDevice device;
        private readonly MouseEventArea mouseArea;
        private readonly FramebufferArea fbArea;
        private readonly IBuiltPipeline builtPipeline;
        private readonly IResourcePool resourcePool;
        private readonly OpenFileModal openFileModal;
        private readonly UniformBuffer<ModelStandardTransformationUniforms> transformUniforms;
        private readonly (string name, Action content)[] infoSections;

        private RWGeometryBuffers? geometryBuffers;
        private ResourceSet[] resourceSets = new ResourceSet[0];
        private UniformBuffer<ModelStandardMaterialUniforms>[] materialUniforms = new UniformBuffer<ModelStandardMaterialUniforms>[0];
        private float distance = 2.0f;
        private Vector2 cameraAngle = Vector2.Zero;
        private bool didSetColumnWidth = false;

        public Window Window { get; }
        public IResource? CurrentResource { get; private set; }

        public ModelViewer(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            device = diContainer.GetTag<GraphicsDevice>();
            resourcePool = diContainer.GetTag<IResourcePool>();
            Window = diContainer.GetTag<WindowContainer>().NewWindow("Model Viewer");
            Window.AddTag(this);
            Window.OnContent += HandleContent;
            var menuBar = new MenuBarWindowTag(Window);
            menuBar.AddItem("Open", HandleMenuOpen);
            builtPipeline = diContainer.GetTag<StandardPipelines>().ModelStandard;
            fbArea = new FramebufferArea(Window, device);
            fbArea.OnRender += HandleRender;
            fbArea.OnResize += HandleResize;
            AddDisposable(fbArea);
            mouseArea = new MouseEventArea(Window);
            mouseArea.OnDrag += HandleDrag;
            mouseArea.OnScroll += HandleScroll;
            openFileModal = new OpenFileModal(diContainer);
            openFileModal.Filter = "*.dff";
            openFileModal.IsFilterChangeable = false;
            openFileModal.OnOpenedResource += LoadModel;

            transformUniforms = new UniformBuffer<ModelStandardTransformationUniforms>(device.ResourceFactory);
            transformUniforms.Ref.view = Matrix4x4.CreateTranslation(Vector3.UnitZ * -2.0f);
            transformUniforms.Ref.world = Matrix4x4.Identity;
            HandleResize(); // sets the projection matrix
            AddDisposable(transformUniforms);

            infoSections = new (string, Action)[]
            {
                ("Statistics", HandleStatisticsContent),
                ("Materials", HandleMaterialsContent),
            };
        }

        public void LoadModel(string pathText)
        {
            var resource = resourcePool.FindFile(pathText);
            if (resource == null)
                throw new FileNotFoundException($"Could not find model at {pathText}");
            LoadModel(resource);
        }

        public void LoadModel(IResource resource)
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
            var geometry = clump.FindChildById(SectionId.Geometry);
            if (geometry == null)
                throw new InvalidDataException("Could not find a geometry section in clump");

            geometryBuffers = new RWGeometryBuffers(diContainer, builtPipeline, (RWGeometry)geometry);
            AddDisposable(geometryBuffers);

            resourceSets = new ResourceSet[geometryBuffers.SubMeshes.Count];
            materialUniforms = new UniformBuffer<ModelStandardMaterialUniforms>[geometryBuffers.SubMeshes.Count];
            foreach (var (rwMaterial, index) in geometryBuffers.SubMeshes.Select(s => s.Material).Indexed())
            {
                var (texture, sampler) = LoadTextureFromMaterial(texturePath, rwMaterial);
                var matUniform = materialUniforms[index] = new UniformBuffer<ModelStandardMaterialUniforms>(device.ResourceFactory);
                matUniform.Ref = ModelStandardMaterialUniforms.Default;
                matUniform.Ref.vertexColorFactor = 0.0f; // they seem to be set to some gray for models?
                matUniform.Ref.tint = rwMaterial.color.ToFColor();
                AddDisposable(matUniform);

                resourceSets[index] = device.ResourceFactory.CreateResourceSet(new ResourceSetDescription
                {
                    Layout = builtPipeline.ResourceLayouts.First(),
                    BoundResources = new BindableResource[]
                    {
                        texture,
                        device.PointSampler,
                        transformUniforms.Buffer,
                        matUniform.Buffer
                    }
                });
                AddDisposable(resourceSets[index]);
            }

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

        private void HandleContent()
        {
            ImGui.Columns(2, null, true);
            if (!didSetColumnWidth)
            {
                ImGui.SetColumnWidth(0, 200.0f);
                didSetColumnWidth = true;
            }
            ImGui.BeginChild("LeftColumn", ImGui.GetContentRegionAvail(), false, ImGuiWindowFlags.HorizontalScrollbar);
            foreach (var (name, content) in infoSections)
            {
                if (!ImGui.CollapsingHeader(name, ImGuiTreeNodeFlags.DefaultOpen))
                    continue;
                
                ImGui.BeginGroup();
                ImGui.Indent();
                content();
                ImGui.EndGroup();

            }
            ImGui.EndChild();

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            ImGui.NextColumn();
            mouseArea.Content();
            fbArea.Content();
            ImGui.PopStyleVar(1);

        }

        private void HandleRender(CommandList cl)
        {
            if (geometryBuffers == null)
                return;
            transformUniforms.Update(cl);

            cl.SetPipeline(builtPipeline.Pipeline);
            geometryBuffers.SetBuffers(cl);
            foreach (var (subMesh, index) in geometryBuffers.SubMeshes.Indexed())
            {
                materialUniforms[index].Update(cl);
                cl.SetGraphicsResourceSet(0, resourceSets[index]);
                cl.DrawIndexed(
                    indexStart: (uint)subMesh.IndexOffset,
                    indexCount: (uint)subMesh.IndexCount,
                    instanceCount: 1,
                    vertexOffset: 0,
                    instanceStart: 0);
            }
        }

        private void HandleResize()
        {
            transformUniforms.Ref.projection = Matrix4x4.CreatePerspectiveFieldOfView(FieldOfView, fbArea.Ratio, 0.01f, 100.0f);
        }

        private void HandleDrag(ImGuiMouseButton button, Vector2 delta)
        {
            if (button != ImGuiMouseButton.Right)
                return;

            cameraAngle += delta * 0.01f;
            while (cameraAngle.X > MathF.PI) cameraAngle.X -= 2 * MathF.PI;
            while (cameraAngle.X < -MathF.PI) cameraAngle.X += 2 * MathF.PI;
            cameraAngle.Y = Math.Clamp(cameraAngle.Y, -MathF.PI / 2.0f, MathF.PI / 2.0f);
            UpdateCamera();
        }

        private void HandleScroll(float scroll)
        {
            distance = distance * MathF.Pow(2.0f, -scroll * 0.1f);
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            var cameraPosition = distance * new Vector3( // TODO: Maybe move this formula to some utility/extension?
                            MathF.Sin(cameraAngle.Y) * MathF.Cos(cameraAngle.X),
                            MathF.Cos(cameraAngle.Y),
                            MathF.Sin(cameraAngle.Y) * MathF.Sin(cameraAngle.X));
            transformUniforms.Ref.view = Matrix4x4.CreateRotationY(cameraAngle.X) * Matrix4x4.CreateRotationX(cameraAngle.Y) * Matrix4x4.CreateTranslation(0.0f, 0.0f, -distance);
            fbArea.IsDirty = true;
        }

        private void HandleStatisticsContent()
        {
            ImGui.Text($"Vertices: {geometryBuffers?.VertexCount}");
            ImGui.Text($"Triangles: {geometryBuffers?.TriangleCount}");
            ImGui.Text($"Submeshes: {geometryBuffers?.SubMeshes.Count}");
        }

        private void HandleMaterialsContent()
        {
            if (geometryBuffers == null)
                return;

            ImGui.Text("Globals");
            var mat = materialUniforms.First().Value;
            bool didChange = false;
            didChange |= ImGui.SliderFloat("Vertex Color Factor", ref mat.vertexColorFactor, 0.0f, 1.0f);
            didChange |= ImGui.SliderFloat("Global Tint Factor", ref mat.tintFactor, 0.0f, 1.0f);
            if (didChange)
            {
                foreach (var matUniform in materialUniforms)
                    matUniform.Ref = mat;
                fbArea.IsDirty = true;
            }

            ImGui.NewLine();
            ImGui.Text("Materials");
            foreach (var (rwMat, index) in geometryBuffers.SubMeshes.Select(s => s.Material).Indexed())
            {
                bool isVisible = materialUniforms[index].Value.alphaReference < 2f;
                ImGui.PushID(index);
                if (ImGui.SmallButton(isVisible ? "V" : "H"))
                {
                    isVisible = !isVisible;
                    materialUniforms[index].Ref.alphaReference = isVisible ? 0.03f : 2.0f;
                    fbArea.IsDirty = true;
                }
                ImGui.PopID();
                ImGui.SameLine();
                if (!ImGui.TreeNodeEx($"Material #{index}", ImGuiTreeNodeFlags.DefaultOpen))
                    continue;
                var color = rwMat.color.ToFColor().ToNumerics();
                ImGui.ColorEdit4("Color", ref color, ImGuiColorEditFlags.NoPicker | ImGuiColorEditFlags.Float);
                ImGui.TreePop();
            }
        }

        private void HandleMenuOpen()
        {
            openFileModal.InitialSelectedResource = CurrentResource;
            openFileModal.Modal.Open();
        }
    }
}
