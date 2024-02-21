using IconFonts;
using ImGuiNET;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzre.debug;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;
using Quaternion = System.Numerics.Quaternion;

namespace zzre.tools;

partial class SceneEditor
{
    private sealed class Sample3D : BaseDisposable, ISelectable
    {
        private const float SelectableSize = 0.2f;

        public Location Location { get; } = new();
        public zzio.scn.Sample3D SceneSample { get; }
        public int Index { get; }

        public string Title => $"#{SceneSample.idx} - {SceneSample.filename}";
        public IRaycastable SelectableBounds => new Sphere(Location.GlobalPosition, SelectableSize);
        public IRaycastable? RenderedBounds => new Sphere(Location.GlobalPosition, SceneSample.maxDist);
        public float ViewSize => SceneSample.maxDist;

        public Sample3D(zzio.scn.Sample3D sceneSample, int index)
        {
            SceneSample = sceneSample;
            Index = index;

            Location.LocalPosition = sceneSample.pos;
            Location.LocalRotation = Quaternion.CreateFromRotationMatrix(
                Matrix4x4.CreateLookAt(Vector3.Zero, sceneSample.forward, sceneSample.up));
        }

        public void Content()
        {
            LabelText("Filename", SceneSample.filename);
            SliderInt("Volume", ref SceneSample.volume, 0, 100);
            DragFloatRange2("Distance", ref SceneSample.minDist, ref SceneSample.maxDist);
            InputInt("Loop count", ref SceneSample.loopCount);
            InputInt("Falloff", ref SceneSample.falloff);
        }
    }

    private sealed class Sample3DComponent : BaseDisposable, IEnumerable<ISelectable>
    {
        private static readonly IColor NormalColor = IColor.White;
        private static readonly IColor SelectedColor = IColor.Red;

        private readonly DebugIconRenderer iconRenderer;
        private readonly IconFont iconFont;
        private readonly SceneEditor editor;

        private Sample3D[] samples = [];
        private bool wasSelected;
        private float iconSize = 128f;

        public Sample3DComponent(ITagContainer diContainer)
        {
            diContainer.AddTag(this);
            editor = diContainer.GetTag<SceneEditor>();
            editor.fbArea.OnRender += HandleRender;
            editor.fbArea.OnResize += HandleResize;
            editor.OnLoadScene += HandleLoadScene;
            editor.OnNewSelection += HandleSelectionEvent;
            editor.OnSelectionManipulate += HandleSelectionEvent;
            editor.editor.AddInfoSection("3D Samples", HandleInfoSection, false);
            editor.selectableContainers.Add(this);
            diContainer.GetTag<MenuBarWindowTag>().AddSlider("View/3D Samples/Size", 0.0f, 512f, () => ref iconSize, UpdateIcons);
            iconFont = diContainer.GetTag<IconFont>();
            iconRenderer = new DebugIconRenderer(diContainer);
            iconRenderer.Material.LinkTransformsTo(diContainer.GetTag<Camera>());
            iconRenderer.Material.World.Ref = Matrix4x4.Identity;
            iconRenderer.Material.MainTexture.Texture = iconFont.Texture;
            iconRenderer.Material.MainSampler.Sampler = iconFont.Sampler;
            HandleResize();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            iconRenderer.Dispose();
            foreach (var sample in samples)
                sample.Dispose();
        }

        private void HandleLoadScene()
        {
            foreach (var oldTrigger in samples)
                oldTrigger.Dispose();
            samples = [];
            if (editor.scene == null)
                return;

            samples = editor.scene.samples3D.Select((s, i) => new Sample3D(s, i)).ToArray();

            UpdateIcons();
        }

        private void HandleResize()
        {
            iconRenderer.Material.ScreenSize.Ref = new Vector2(
                editor.fbArea.Framebuffer.Width, editor.fbArea.Framebuffer.Height);
        }

        private void HandleRender(CommandList cl)
        {
            if (iconSize > 0f)
                iconRenderer.Render(cl);
        }

        private void HandleSelectionEvent(ISelectable? selectable)
        {
            var prevWasSelected = wasSelected;
            wasSelected = selectable is Trigger;
            if (prevWasSelected)
                UpdateIcons();
        }

        private void UpdateIcons()
        {
            iconRenderer.Icons = samples.Select(GetDebugIconFor).ToArray();
            editor.fbArea.IsDirty = true;
        }

        private DebugIcon GetDebugIconFor(Sample3D sample)
        {
            var glyph = iconFont.Glyphs[ForkAwesome.VolumeUp];
            return new DebugIcon
            {
                pos = sample.Location.GlobalPosition,
                uvPos = glyph.Min,
                uvSize = glyph.Size,
                size = Vector2.One * iconSize,
                color = editor.Selected == sample ? SelectedColor : NormalColor,
                textureWeight = 1f
            };
        }

        private void HandleInfoSection()
        {
            foreach (var (sample, index) in samples.Indexed())
            {
                var flags =
                    ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick |
                    (sample == editor.Selected ? ImGuiTreeNodeFlags.Selected : 0);
                var isOpen = TreeNodeEx(sample.Title, flags);
                if (IsItemClicked())
                    editor.Selected = sample;
                if (IsItemClicked() && IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    editor.MoveCameraToSelected();
                if (!isOpen)
                    continue;
                PushID(index);
                sample.Content();
                PopID();
                TreePop();
            }
        }

        public IEnumerator<ISelectable> GetEnumerator() => ((IEnumerable<ISelectable>)samples).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
