using IconFonts;
using ImGuiNET;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio;
using zzio.scn;
using zzre.debug;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using static zzio.scn.SceneEffectType;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;
using Quaternion = System.Numerics.Quaternion;

namespace zzre.tools;

partial class SceneEditor
{
    private sealed class Effect : BaseDisposable, ISelectable
    {
        private const float PointTriggerSize = 0.1f;
        private const float SelectableSize = 0.2f;

        private readonly ITagContainer diContainer;

        public Location Location { get; } = new Location();
        public zzio.scn.SceneEffect SceneEffect { get; }
        public int Index { get; }

        public string Title => $"#{SceneEffect.idx} - {SceneEffect.type}";
        public bool IsBoxEffect => SceneEffect.type is Leaves or Unused5 or Unknown6 or Unknown10;
        public IRaycastable SelectableBounds => new Sphere(Location.GlobalPosition, SelectableSize);

        public IRaycastable? RenderedBounds => IsBoxEffect
            ? Box.FromMinMax(Location.GlobalPosition, Location.GlobalPosition + SceneEffect.end - SceneEffect.pos)
            : null;

        public float ViewSize => IsBoxEffect ? (SceneEffect.end - SceneEffect.pos).MaxComponent() : PointTriggerSize * 2f;

        public Effect(ITagContainer diContainer, SceneEffect sceneEffect, int index)
        {
            this.diContainer = diContainer;
            SceneEffect = sceneEffect;
            Index = index;

            Location.LocalPosition = sceneEffect.pos;
            Location.LocalRotation = Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateLookAt(
                Vector3.Zero,
                SafeDirection(sceneEffect.dir, Vector3.UnitZ),
                SafeDirection(sceneEffect.up, Vector3.UnitY)));
        }

        private static Vector3 SafeDirection(Vector3 data, Vector3 safe) =>
            MathEx.CmpZero(data.LengthSquared()) ? safe : Vector3.Normalize(data);

        public void Content()
        {
            EnumCombo("Type", ref SceneEffect.type);
            EnumCombo("Order", ref SceneEffect.order);
            InputInt("Param", ref SceneEffect.param);

            switch(SceneEffect.type)
            {
                case Leaves:
                case Unused5:
                case Unknown6:
                case Unknown10:
                    InputFloat3("AABB Min", ref SceneEffect.pos);
                    InputFloat3("AABB Max", ref SceneEffect.end);
                    break;
                case Unused4:
                case Unused7:
                    InputFloat3("Pos", ref SceneEffect.pos);
                    break;
                case Combiner:
                    InputFloat3("Pos", ref SceneEffect.pos);
                    InputFloat3("Dir", ref SceneEffect.dir);
                    InputFloat3("Up", ref SceneEffect.up);
                    if (Hyperlink("Effect", SceneEffect.effectFile))
                    {
                        var fullPath = new FilePath("resources/effects/").Combine(SceneEffect.effectFile + ".ed");
                        diContainer.GetTag<OpenDocumentSet>().Open(fullPath);
                    }
                    break;
            }
        }
    }

    private sealed class EffectComponent : BaseDisposable, IEnumerable<ISelectable>
    {
        private static readonly IColor NormalColor = IColor.White;
        private static readonly IColor SelectedColor = IColor.Red;

        private readonly ITagContainer diContainer;
        private readonly DebugIconRenderer iconRenderer;
        private readonly IconFont iconFont;
        private readonly SceneEditor editor;

        private Effect[] effects = [];
        private bool wasSelected;
        private float iconSize = 128f;

        public EffectComponent(ITagContainer diContainer)
        {
            diContainer.AddTag(this);
            this.diContainer = diContainer;
            editor = diContainer.GetTag<SceneEditor>();
            editor.fbArea.OnRender += HandleRender;
            editor.fbArea.OnResize += HandleResize;
            editor.OnLoadScene += HandleLoadScene;
            editor.OnNewSelection += HandleSelectionEvent;
            editor.OnSelectionManipulate += HandleSelectionEvent;
            editor.editor.AddInfoSection("Effects", HandleInfoSection, false);
            editor.selectableContainers.Add(this);
            diContainer.GetTag<MenuBarWindowTag>().AddSlider("View/Effects/Size", 0.0f, 512f, () => ref iconSize, UpdateIcons);
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
            foreach (var effect in effects)
                effect.Dispose();
        }

        private void HandleLoadScene()
        {
            foreach (var oldEffect in effects)
                oldEffect.Dispose();
            effects = [];
            if (editor.scene == null)
                return;

            effects = editor.scene.effects.Select((e, i) => new Effect(diContainer, e, i)).ToArray();

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
            iconRenderer.Icons = effects.Select(GetDebugIconFor).ToArray();
            editor.fbArea.IsDirty = true;
        }

        private DebugIcon GetDebugIconFor(Effect effect)
        {
            var glyph = iconFont.Glyphs[effect.SceneEffect.type switch
            {
                Snowflakes => ForkAwesome.SnowflakeO,
                Leaves => ForkAwesome.Leaf,
                _ => ForkAwesome.Bolt
            }];
            return new DebugIcon
            {
                pos = effect.Location.GlobalPosition,
                uvPos = glyph.Min,
                uvSize = glyph.Size,
                size = Vector2.One * iconSize,
                color = editor.Selected == effect ? SelectedColor : NormalColor,
                textureWeight = 1f
            };
        }

        private void HandleInfoSection()
        {
            foreach (var (effect, index) in effects.Indexed())
            {
                var flags =
                    ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick |
                    (effect == editor.Selected ? ImGuiTreeNodeFlags.Selected : 0);
                var isOpen = TreeNodeEx(effect.Title, flags);
                if (IsItemClicked())
                    editor.Selected = effect;
                if (IsItemClicked() && IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    editor.MoveCameraToSelected();
                if (!isOpen)
                    continue;
                PushID(index);
                effect.Content();
                PopID();
                TreePop();
            }
        }

        public IEnumerator<ISelectable> GetEnumerator() => ((IEnumerable<ISelectable>)effects).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
