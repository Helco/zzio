using IconFonts;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Veldrid;
using zzio.primitives;
using zzio.scn;
using zzre.core.rendering;
using zzre.debug;
using zzre.imgui;
using zzre.materials;
using zzre.rendering;
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;
using Quaternion = System.Numerics.Quaternion;

namespace zzre.tools
{
    public partial class SceneEditor
    {
        private class Trigger : BaseDisposable, ISelectable
        {
            private const float PointTriggerSize = 0.1f;

            private readonly ITagContainer diContainer;

            public Location Location { get; } = new Location();
            public zzio.scn.Trigger SceneTrigger { get; }
            public int Index { get; }

            public string Title => $"#{SceneTrigger.idx} - {SceneTrigger.type}";
            public Bounds Bounds { get; }

            public Trigger(ITagContainer diContainer, zzio.scn.Trigger sceneTrigger, int index)
            {
                this.diContainer = diContainer;
                SceneTrigger = sceneTrigger;
                Index = index;

                Location.LocalPosition = sceneTrigger.pos.ToNumerics();
                Location.LocalRotation = Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateLookAt(Vector3.Zero, sceneTrigger.dir.ToNumerics(), Vector3.UnitY));

                Bounds = new Bounds(Vector3.Zero, SceneTrigger.colliderType switch
                {
                    TriggerColliderType.Box => SceneTrigger.size.ToNumerics(),
                    TriggerColliderType.Sphere => Vector3.One * SceneTrigger.radius,
                    TriggerColliderType.Point => Vector3.One * PointTriggerSize,
                    _ => throw new NotImplementedException("Unknown TriggerColliderType")
                });
            }

            public void Content()
            {
                bool hasChanged = false;
                hasChanged |= InputInt("Desc1", ref SceneTrigger.ii1);
                hasChanged |= InputInt("Desc2", ref SceneTrigger.ii2);
                hasChanged |= InputInt("Desc3", ref SceneTrigger.ii3);
                hasChanged |= InputInt("Desc4", ref SceneTrigger.ii4);
                hasChanged |= InputText("S", ref SceneTrigger.s, 256);
            }
        }

        private class TriggerComponent : BaseDisposable
        {
            private static readonly IColor NormalColor = IColor.White;
            private static readonly IColor SelectedColor = IColor.Red;

            private readonly ITagContainer diContainer;
            private readonly DebugIconRenderer iconRenderer;
            private readonly IconFont iconFont;
            private readonly SceneEditor editor;

            private Trigger[] triggers = new Trigger[0];
            private bool isVisible = true;
            private float iconSize = 196f;

            public TriggerComponent(ITagContainer diContainer)
            {
                diContainer.AddTag(this);
                this.diContainer = diContainer;
                editor = diContainer.GetTag<SceneEditor>();
                editor.fbArea.OnRender += HandleRender;
                editor.fbArea.OnResize += HandleResize;
                editor.OnLoadScene += HandleLoadScene;
                editor.OnNewSelection += _ => UpdateIcons();
                editor.editor.AddInfoSection("Triggers", HandleInfoSection, false);
                diContainer.GetTag<MenuBarWindowTag>().AddSlider("View/Triggers/Size", 0.0f, 512f, () => ref iconSize, UpdateIcons);
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
                foreach (var trigger in triggers)
                    trigger.Dispose();
            }

            private void HandleLoadScene()
            {
                foreach (var oldTrigger in triggers)
                    oldTrigger.Dispose();
                triggers = new Trigger[0];
                if (editor.scene == null)
                    return;

                triggers = editor.scene.triggers.Select((t, i) => new Trigger(diContainer, t, i)).ToArray();

                UpdateIcons();
            }

            private void HandleResize()
            {
                iconRenderer.Material.Uniforms.Ref.screenSize = new Vector2(
                    editor.fbArea.Framebuffer.Width, editor.fbArea.Framebuffer.Height);
            }

            private void HandleRender(CommandList cl)
            {
                if (!isVisible)
                    return;
                iconRenderer.Render(cl);
            }

            private void UpdateIcons()
            {
                iconRenderer.Icons = triggers.Select(GetDebugIconFor).ToArray();
                editor.fbArea.IsDirty = true;
            }

            private DebugIcon GetDebugIconFor(Trigger trigger)
            {
                var glyph = iconFont.Glyphs[ForkAwesome.Bell];
                return new DebugIcon
                {
                    pos = trigger.Location.GlobalPosition,
                    uvCenter = glyph.Center,
                    uvSize = glyph.Size,
                    size = iconSize,
                    color = editor.Selected == trigger ? SelectedColor : NormalColor
                };
            }

            private void HandleInfoSection()
            {
                foreach (var (trigger, index) in triggers.Indexed())
                {
                    var flags =
                        ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick |
                        (trigger == editor.Selected ? ImGuiTreeNodeFlags.Selected : 0);
                    var isOpen = TreeNodeEx(trigger.Title, flags);
                    if (IsItemClicked())
                        editor.Selected = trigger;
                    if (IsItemClicked() && IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        editor.MoveCameraToSelected();
                    if (!isOpen)
                        continue;
                    PushID(index);
                    trigger.Content();
                    PopID();
                    TreePop();
                }
            }
        }
    }
}
