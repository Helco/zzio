using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using IconFonts;
using ImGuiNET;
using System.Runtime.CompilerServices;
using Veldrid;
using zzio;
using zzio.scn;
using zzre.debug;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;

namespace zzre.tools;

partial class SceneEditor
{
    private class Light : BaseDisposable, ISelectable
    {
        private const float PointTriggerSize = 0.1f;
        private const float SelectableSize = 0.2f;

        private readonly ITagContainer diContainer;

        public Location Location { get; } = new Location();
        public zzio.scn.Light SceneLight { get; }
        public int Index { get; }

        public string Title => $"#{SceneLight.idx} - {SceneLight.type}";
        public IRaycastable SelectableBounds => new Sphere(Location.GlobalPosition, SelectableSize);

        public IRaycastable? RenderedBounds => SceneLight.type switch
        {
            LightType.Point => new Sphere(Location.GlobalPosition, SceneLight.radius),
            _ => null
        };

        public float ViewSize => Math.Max(SceneLight.radius, SelectableSize) * 2;

        public Light(ITagContainer diContainer, zzio.scn.Light light, int index)
        {
            this.diContainer = diContainer;
            SceneLight = light;
            Index = index;

            Location.LocalPosition = light.pos;
            if (light.type is LightType.Directional or LightType.Spot)
                Location.LocalRotation = light.vec.ToZZRotation();
        }

        public void Content()
        {
            InputInt("Idx", ref SceneLight.idx);
            EnumCombo("Type", ref SceneLight.type);
            FlagsCombo("Flags", ref SceneLight.flags);
            ColorEdit4("Color", ref SceneLight.color);
            DragFloat("Radius", ref SceneLight.radius);
        }
    }

    private class LightComponent : BaseDisposable, IEnumerable<ISelectable>
    {
        private static readonly IColor NormalColor = IColor.White;
        private static readonly IColor SelectedColor = IColor.Red;

        private readonly ITagContainer diContainer;
        private readonly DebugIconRenderer iconRenderer;
        private readonly IconFont iconFont;
        private readonly SceneEditor editor;

        private Light[] lights = Array.Empty<Light>();
        private bool wasSelected = false;
        private float iconSize = 128f;

        public LightComponent(ITagContainer diContainer)
        {
            diContainer.AddTag(this);
            this.diContainer = diContainer;
            editor = diContainer.GetTag<SceneEditor>();
            editor.fbArea.OnRender += HandleRender;
            editor.fbArea.OnResize += HandleResize;
            editor.OnLoadScene += HandleLoadScene;
            editor.OnNewSelection += HandleSelectionEvent;
            editor.OnSelectionManipulate += HandleSelectionEvent;
            editor.editor.AddInfoSection("Lights", HandleInfoSection, false);
            editor.selectableContainers.Add(this);
            diContainer.GetTag<MenuBarWindowTag>().AddSlider("View/Lights/Size", 0.0f, 512f, () => ref iconSize, UpdateIcons);
            iconFont = diContainer.GetTag<IconFont>();
            iconRenderer = new DebugIconRenderer(diContainer);
            iconRenderer.Material.LinkTransformsTo(diContainer.GetTag<Camera>());
            iconRenderer.Material.World.Ref = Matrix4x4.Identity;
            iconRenderer.Material.Texture.Texture = iconFont.Texture;
            iconRenderer.Material.Sampler.Sampler = iconFont.Sampler;
            HandleResize();
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            iconRenderer.Dispose();
            foreach (var light in lights)
                light.Dispose();
        }

        private void HandleLoadScene()
        {
            foreach (var oldLight in lights)
                oldLight.Dispose();
            lights = Array.Empty<Light>();
            if (editor.scene == null)
                return;

            lights = editor.scene.lights.Select((l, i) => new Light(diContainer, l, i)).ToArray();

            UpdateIcons();
        }

        private void HandleResize()
        {
            iconRenderer.Material.Uniforms.Ref.screenSize = new Vector2(
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
            iconRenderer.Icons = lights.Select(GetDebugIconFor).ToArray();
            editor.fbArea.IsDirty = true;
        }

        private DebugIcon GetDebugIconFor(Light light)
        {
            var glyph = iconFont.Glyphs[ForkAwesome.LightbulbO];
            return new DebugIcon
            {
                pos = light.Location.GlobalPosition,
                uvCenter = glyph.Center,
                uvSize = glyph.Size,
                size = iconSize,
                color = editor.Selected == light ? SelectedColor : NormalColor
            };
        }

        private void HandleInfoSection()
        {
            foreach (var (light, index) in lights.Indexed())
            {
                var flags =
                    ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick |
                    (light == editor.Selected ? ImGuiTreeNodeFlags.Selected : 0);
                var isOpen = TreeNodeEx(light.Title, flags);
                if (IsItemClicked())
                    editor.Selected = light;
                if (IsItemClicked() && IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    editor.MoveCameraToSelected();
                if (!isOpen)
                    continue;
                PushID(index);
                light.Content();
                PopID();
                TreePop();
            }
        }

        public IEnumerator<ISelectable> GetEnumerator() => ((IEnumerable<ISelectable>)lights).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
