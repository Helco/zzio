using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Veldrid;
using ImGuiNET;
using zzio;
using zzio.vfs;
using zzre.rendering;
using zzre.materials;
using zzre.debug;
using zzre.imgui;
using System.Collections.Generic;

namespace zzre.tools
{
    public class ModelViewer : ListDisposable, IDocumentEditor
    {
        private const byte DebugPlaneAlpha = 0xA0;

        private readonly ITagContainer diContainer;
        private readonly TwoColumnEditorTag editor;
        private readonly Camera camera;
        private readonly OrbitControlsTag controls;
        private readonly ImGuiRenderer imGuiRenderer;
        private readonly GraphicsDevice device;
        private readonly FramebufferArea fbArea;
        private readonly IAssetLoader<Texture> textureLoader;
        private readonly IResourcePool resourcePool;
        private readonly DebugGridRenderer gridRenderer;
        private readonly DebugTriangleLineRenderer triangleRenderer;
        private readonly DebugPlaneRenderer planeRenderer;
        private readonly OpenFileModal openFileModal;
        private readonly ModelMaterialEdit modelMaterialEdit;
        private readonly LocationBuffer locationBuffer;

        private ClumpBuffers? geometryBuffers;
        private GeometryTreeCollider? collider;
        private ModelStandardMaterial[] materials = Array.Empty<ModelStandardMaterial>();
        private DebugSkeletonRenderer? skeletonRenderer;
        private int highlightedSplitI = -1;

        public Window Window { get; }
        public IResource? CurrentResource { get; private set; }

        public ModelViewer(ITagContainer diContainer)
        {
            this.diContainer = diContainer;
            device = diContainer.GetTag<GraphicsDevice>();
            resourcePool = diContainer.GetTag<IResourcePool>();
            textureLoader = diContainer.GetTag<IAssetLoader<Texture>>();
            Window = diContainer.GetTag<WindowContainer>().NewWindow("Model Viewer");
            Window.InitialBounds = new Rect(float.NaN, float.NaN, 1100.0f, 600.0f);
            Window.AddTag(this);
            editor = new TwoColumnEditorTag(Window, diContainer);
            var onceAction = new OnceAction();
            Window.AddTag(onceAction);
            Window.OnContent += onceAction.Invoke;
            var menuBar = new MenuBarWindowTag(Window);
            menuBar.AddButton("Open", HandleMenuOpen);
            fbArea = Window.GetTag<FramebufferArea>();
            fbArea.OnResize += HandleResize;
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

            var localDiContainer = diContainer.ExtendedWith(locationBuffer);
            camera = new Camera(localDiContainer);
            AddDisposable(camera);

            controls = new OrbitControlsTag(Window, camera.Location, localDiContainer);
            AddDisposable(controls);

            gridRenderer = new DebugGridRenderer(diContainer);
            gridRenderer.Material.LinkTransformsTo(camera);
            gridRenderer.Material.World.Ref = Matrix4x4.Identity;
            AddDisposable(gridRenderer);

            triangleRenderer = new DebugTriangleLineRenderer(diContainer);
            triangleRenderer.Material.LinkTransformsTo(camera);
            triangleRenderer.Material.World.Ref = Matrix4x4.Identity;
            AddDisposable(triangleRenderer);

            planeRenderer = new DebugPlaneRenderer(diContainer);
            planeRenderer.Material.LinkTransformsTo(camera);
            planeRenderer.Material.World.Ref = Matrix4x4.Identity;
            AddDisposable(planeRenderer);

            editor.AddInfoSection("Statistics", HandleStatisticsContent);
            editor.AddInfoSection("Materials", HandleMaterialsContent);
            editor.AddInfoSection("Skeleton", HandleSkeletonContent);
            editor.AddInfoSection("Collision", HandleCollisionContent);
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
            var texturePaths = new[]
            {
                textureLoader.GetTexturePathFromModel(resource.Path),
                new FilePath("resources/textures/models"),
                new FilePath("resources/textures/worlds"),
            };

            geometryBuffers = new ClumpBuffers(diContainer, resource);
            AddDisposable(geometryBuffers);

            foreach (var oldTexture in materials.Select(m => m.MainTexture.Texture))
                imGuiRenderer.RemoveImGuiBinding(oldTexture);

            materials = new ModelStandardMaterial[geometryBuffers.SubMeshes.Count];
            foreach (var (rwMaterial, index) in geometryBuffers.SubMeshes.Select(s => s.Material).Indexed())
            {
                var material = materials[index] = new ModelStandardMaterial(diContainer);
                (material.MainTexture.Texture, material.Sampler.Sampler) = textureLoader.LoadTexture(texturePaths, rwMaterial);
                material.LinkTransformsTo(camera);
                material.World.Ref = Matrix4x4.Identity;
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
                skeletonRenderer = new DebugSkeletonRenderer(diContainer.ExtendedWith(camera, locationBuffer), geometryBuffers, skeleton);
                AddDisposable(skeletonRenderer);
            }

            collider = geometryBuffers.RWGeometry.FindChildById(zzio.rwbs.SectionId.CollisionPLG, true) == null
                ? null
                : new GeometryTreeCollider(geometryBuffers.RWGeometry, location: null);

            controls.ResetView();
            HighlightSplit(-1);
            fbArea.IsDirty = true;
            CurrentResource = resource;
            Window.Title = $"Model Viewer - {resource.Path.ToPOSIXString()}";
        }

        private void HandleResize() => camera.Aspect = fbArea.Ratio;

        private void HandleRender(CommandList cl)
        {
            locationBuffer.Update(cl);
            camera.Update(cl);
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
            planeRenderer.Render(cl);
            triangleRenderer.Render(cl);
        }

        private void HandleStatisticsContent()
        {
            ImGui.Text($"Vertices: {geometryBuffers?.VertexCount}");
            ImGui.Text($"Triangles: {geometryBuffers?.TriangleCount}");
            ImGui.Text($"Submeshes: {geometryBuffers?.SubMeshes.Count}");
            ImGui.Text($"Bones: {skeletonRenderer?.Skeleton.Bones.Count}");
            ImGui.Text($"Collision splits: {collider?.Collision.splits.Length}");
            ImGui.Text("Collision test: " + (geometryBuffers?.IsSolid ?? false ? "yes" : "no"));
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

        private void HighlightSplit(int splitI)
        {
            highlightedSplitI = splitI;
            triangleRenderer.Triangles = Array.Empty<Triangle>();
            planeRenderer.Planes = Array.Empty<DebugPlane>();
            if (collider == null || geometryBuffers == null || highlightedSplitI < 0)
                return;

            var split = collider.Collision.splits[splitI];
            var normal = split.left.type switch
            {
                zzio.rwbs.CollisionSectorType.X => Vector3.UnitX,
                zzio.rwbs.CollisionSectorType.Y => Vector3.UnitY,
                zzio.rwbs.CollisionSectorType.Z => Vector3.UnitZ,
                _ => throw new NotSupportedException($"Unsupported collision sector type: {split.left.type}")
            };
            SetPlanes(geometryBuffers.Bounds, normal, split.left.value, split.right.value, centerValue: null);

            triangleRenderer.Triangles = SplitTriangles(split).ToArray();
            triangleRenderer.Colors = Enumerable
                .Repeat(IColor.Red, SectorTriangles(split.left).Count())
                .Concat(Enumerable
                    .Repeat(IColor.Blue, SectorTriangles(split.right).Count()))
                .ToArray();

            fbArea.IsDirty = true;

            IEnumerable<Triangle> SplitTriangles(zzio.rwbs.CollisionSplit split) =>
                SectorTriangles(split.left).Concat(SectorTriangles(split.right));

            IEnumerable<Triangle> SectorTriangles(zzio.rwbs.CollisionSector sector) => sector.count == zzio.rwbs.RWCollision.SplitCount
                ? SplitTriangles(collider.Collision.splits[sector.index])
                : collider.Collision.map
                    .Skip(sector.index)
                    .Take(sector.count)
                    .Select(collider.GetTriangle)
                    .ToArray();
        }

        private void SetPlanes(Box bounds, Vector3 normal, float leftValue, float rightValue, float? centerValue)
        {
            var planarCenter = bounds.Center * (Vector3.One - normal);
            var otherSizes = bounds.Size * (Vector3.One - normal);
            var size = Math.Max(Math.Max(otherSizes.X, otherSizes.Y), otherSizes.Z) * 0.5f;
            planeRenderer.Planes = new[]
            {
                    new DebugPlane()
                    {
                        center = planarCenter + normal * leftValue,
                        normal = normal,
                        size = size * 0.7f,
                        color = IColor.Red.WithA(DebugPlaneAlpha)
                    },
                    new DebugPlane()
                    {
                        center = planarCenter + normal * rightValue,
                        normal = normal,
                        size = size * 0.7f,
                        color = IColor.Blue.WithA(DebugPlaneAlpha)
                    }
            };
            if (centerValue.HasValue)
            {
                planeRenderer.Planes = planeRenderer.Planes.Append(
                    new DebugPlane()
                    {
                        center = planarCenter + normal * centerValue.Value,
                        normal = normal,
                        size = size,
                        color = IColor.Green.WithA(DebugPlaneAlpha)
                    }).ToArray();
            }
        }

        private void HandleCollisionContent()
        {
            if (geometryBuffers == null)
            {
                ImGui.Text("No model loaded");
                return;
            }
            else if (collider == null)
            {
                ImGui.Text("No collision in model");
                return;
            }

            Split(0);
            void Split(int splitI)
            {
                var split = collider.Collision.splits[splitI];
                var flags = (splitI == highlightedSplitI ? ImGuiTreeNodeFlags.Selected : 0) |
                    ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.OpenOnArrow |
                    ImGuiTreeNodeFlags.DefaultOpen;
                var isOpen = ImGui.TreeNodeEx($"{split.left.type} {split.left.value}-{split.right.value}", flags);
                if (ImGui.IsItemClicked() && splitI != highlightedSplitI)
                    HighlightSplit(splitI);

                if (isOpen)
                {
                    Sector(split.left, false);
                    Sector(split.right, true);
                    ImGui.TreePop();
                }
            }

            void Sector(zzio.rwbs.CollisionSector sector, bool isRight)
            {
                if (sector.count == zzio.rwbs.RWCollision.SplitCount)
                    Split(sector.index);
                else
                    ImGui.Text($"{(isRight ? "Right" : "Left")}: {sector.count} Triangles");
            }
        }
    }
}
