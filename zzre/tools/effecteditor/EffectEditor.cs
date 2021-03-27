using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Veldrid;
using zzio.effect;
using zzio.effect.parts;
using zzio.vfs;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using zzre.rendering.effectparts;

namespace zzre.tools
{
    public partial class EffectEditor : ListDisposable, IDocumentEditor
    {
        private readonly ITagContainer diContainer;
        private readonly TwoColumnEditorTag editor;
        private readonly Camera camera;
        private readonly OrbitControlsTag controls;
        private readonly GraphicsDevice device;
        private readonly FramebufferArea fbArea;
        private readonly IResourcePool resourcePool;
        private readonly DebugGridRenderer gridRenderer;
        private readonly OpenFileModal openFileModal;
        private readonly LocationBuffer locationBuffer;

        private EffectCombinerRenderer? effectRenderer;
        private EffectCombiner? Effect => effectRenderer?.Effect;
        private bool[] isVisible = Array.Empty<bool>();

        public Window Window { get; }
        public IResource? CurrentResource { get; private set; }

        public EffectEditor(ITagContainer diContainer)
        {
            device = diContainer.GetTag<GraphicsDevice>();
            resourcePool = diContainer.GetTag<IResourcePool>();
            Window = diContainer.GetTag<WindowContainer>().NewWindow("Effect Editor");
            Window.InitialBounds = new Rect(float.NaN, float.NaN, 1100f, 600f);
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
            diContainer.GetTag<OpenDocumentSet>().AddEditor(this);

            openFileModal = new OpenFileModal(diContainer)
            {
                Filter = "*.ed",
                IsFilterChangeable = false
            };
            openFileModal.OnOpenedResource += Load;

            locationBuffer = new LocationBuffer(device);
            this.diContainer = diContainer.ExtendedWith(locationBuffer);
            AddDisposable(this.diContainer);
            this.diContainer.AddTag(camera = new Camera(this.diContainer));
            this.diContainer.AddTag<IQuadMeshBuffer<EffectVertex>>(new DynamicQuadMeshBuffer<EffectVertex>(device.ResourceFactory));
            controls = new OrbitControlsTag(Window, camera.Location, this.diContainer);
            AddDisposable(controls);
            gridRenderer = new DebugGridRenderer(this.diContainer);
            gridRenderer.Material.LinkTransformsTo(camera);
            gridRenderer.Material.World.Ref = Matrix4x4.Identity;
            AddDisposable(gridRenderer);

            editor.AddInfoSection("Info", HandleInfoContent);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            effectRenderer?.Dispose();
        }

        public void Load(string pathText)
        {
            var resource = resourcePool.FindFile(pathText);
            if (resource == null)
                throw new FileNotFoundException($"Could not find model at {pathText}");
            Load(resource);
        }

        public void Load(IResource resource) =>
            Window.GetTag<OnceAction>().Next += () => LoadEffectNow(resource);

        private void LoadEffectNow(IResource resource)
        {
            if (resource.Equals(CurrentResource))
                return;
            CurrentResource = null;

            effectRenderer?.Dispose();
            effectRenderer = new EffectCombinerRenderer(diContainer, resource);

            editor.ClearInfoSections();
            editor.AddInfoSection("Info", HandleInfoContent);
            foreach (var (partRenderer, i) in effectRenderer.Parts.Indexed())
            {
                var part = Effect!.parts[i];
                editor.AddInfoSection($"{part.Type} \"{part.Name}\"", part switch
                {
                    MovingPlanes mp => () => HandlePartMovingPlanes(mp, (MovingPlanesRenderer)partRenderer),
                    _ => () => { } // ignore for now
                }, defaultOpen: false, () => HandlePartPreContent(i));
            }
            isVisible = Enumerable.Repeat(true, effectRenderer.Parts.Count).ToArray();

            controls.ResetView();
            fbArea.IsDirty = true;
            CurrentResource = resource;
            Window.Title = $"Effect Editor - {resource.Path.ToPOSIXString()}";
        }

        private void HandleResize() => camera.Aspect = fbArea.Ratio;

        private void HandleRender(CommandList cl)
        {
            locationBuffer.Update(cl);
            camera.Update(cl);
            gridRenderer.Render(cl);

            if (effectRenderer == null)
                return;
            foreach (var part in effectRenderer.Parts.Where((p, i) => isVisible[i]))
                part.Render(cl);
        }

        private void HandleInfoContent()
        {
            var descr = Effect?.description ?? "";
            ImGui.InputText("Description", ref descr, 512);

            var pos = Effect?.position.ToNumerics() ?? Vector3.Zero;
            var forwards = Effect?.forwards.ToNumerics() ?? Vector3.Zero;
            var upwards = Effect?.upwards.ToNumerics() ?? Vector3.Zero;
            ImGui.DragFloat3("Position", ref pos);
            ImGui.DragFloat3("Forwards", ref forwards);
            ImGui.DragFloat3("Upwards", ref upwards);
            var isLooping = Effect?.isLooping ?? false;
            ImGui.Checkbox("Looping", ref isLooping);
        }

        private void HandleMenuOpen()
        {
            openFileModal.InitialSelectedResource = CurrentResource;
            openFileModal.Modal.Open();
        }

        private void HandlePartPreContent(int i)
        {
            if (ImGui.Button(isVisible[i] ? IconFonts.ForkAwesome.Eye : IconFonts.ForkAwesome.EyeSlash))
            {
                isVisible[i] = !isVisible[i];
                fbArea.IsDirty = true;
            }
        }
    }
}
