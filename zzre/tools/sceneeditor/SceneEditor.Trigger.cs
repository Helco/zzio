using IconFonts;
using ImGuiNET;
using System;
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
using static ImGuiNET.ImGui;
using static zzre.imgui.ImGuiEx;
using Quaternion = System.Numerics.Quaternion;

namespace zzre.tools;

public partial class SceneEditor
{
    private sealed class Trigger : BaseDisposable, ISelectable
    {
        private const float PointTriggerSize = 0.1f;
        private const float SelectableSize = 0.2f;

        public Location Location { get; } = new Location();
        public zzio.scn.Trigger SceneTrigger { get; }
        public int Index { get; }

        public string Title => $"#{SceneTrigger.idx} - {SceneTrigger.type}";
        public IRaycastable SelectableBounds => new Sphere(Location.GlobalPosition, SelectableSize);

        public IRaycastable? RenderedBounds => SceneTrigger.colliderType switch
        {
            TriggerColliderType.Box => Box.FromMinMax(Location.GlobalPosition, Location.GlobalPosition + SceneTrigger.end - SceneTrigger.pos),
            TriggerColliderType.Sphere => new Sphere(Location.GlobalPosition, SceneTrigger.radius),
            TriggerColliderType.Point => null,
            _ => throw new NotImplementedException("Unknown TriggerColliderType")
        };
        public float ViewSize => SceneTrigger.colliderType switch
        {
            TriggerColliderType.Box => (SceneTrigger.end - SceneTrigger.pos).MaxComponent(),
            TriggerColliderType.Sphere => SceneTrigger.radius * 2f,
            TriggerColliderType.Point => PointTriggerSize * 2f,
            _ => throw new NotImplementedException("Unknown TriggerColliderType")
        };

        public Trigger(zzio.scn.Trigger sceneTrigger, int index)
        {
            SceneTrigger = sceneTrigger;
            Index = index;

            Location.LocalPosition = sceneTrigger.pos;
            Location.LocalRotation = Quaternion.CreateFromRotationMatrix(Matrix4x4.CreateLookAt(Vector3.Zero, sceneTrigger.dir, Vector3.UnitY));
        }

        public void Content()
        {
            Checkbox("Requires looking", ref SceneTrigger.requiresLooking);
            InputInt("Desc1", ref SceneTrigger.ii1);
            InputInt("Desc2", ref SceneTrigger.ii2);
            InputInt("Desc3", ref SceneTrigger.ii3);
            InputInt("Desc4", ref SceneTrigger.ii4);
            InputText("S", ref SceneTrigger.s, 256);
            EnumCombo("Collider", ref SceneTrigger.colliderType);
            switch (SceneTrigger.colliderType)
            {
                case TriggerColliderType.Sphere:
                    InputFloat("Radius", ref SceneTrigger.radius);
                    break;

                case TriggerColliderType.Box:
                    var size = SceneTrigger.end - SceneTrigger.pos;
                    InputFloat3("Size", ref size);
                    break;
            }
        }

        public void SyncWithScene()
        {
            //Console.WriteLine(Location.LocalPosition.ToString());
            SceneTrigger.pos = Location.LocalPosition;
        }
    }

    private sealed class TriggerComponent : BaseDisposable, IEnumerable<ISelectable>
    {
        private static readonly IColor NormalColor = IColor.White;
        private static readonly IColor SelectedColor = IColor.Red;

        private readonly DebugIconRenderer iconRenderer;
        private readonly IconFont iconFont;
        private readonly SceneEditor editor;

        private Trigger[] triggers = [];
        private bool wasSelected;
        private float iconSize = 128f;

        public TriggerComponent(ITagContainer diContainer)
        {
            diContainer.AddTag(this);
            editor = diContainer.GetTag<SceneEditor>();
            editor.fbArea.OnRender += HandleRender;
            editor.fbArea.OnResize += HandleResize;
            editor.OnLoadScene += HandleLoadScene;
            editor.OnNewSelection += HandleSelectionEvent;
            editor.OnSelectionManipulate += HandleSelectionEvent;
            editor.editor.AddInfoSection("Triggers", HandleInfoSection, false);
            editor.selectableContainers.Add(this);
            diContainer.GetTag<MenuBarWindowTag>().AddSlider("View/Triggers/Size", 0.0f, 512f, () => ref iconSize, UpdateIcons);
            iconFont = diContainer.GetTag<IconFont>();
            iconRenderer = new DebugIconRenderer(diContainer);
            iconRenderer.Material.LinkTransformsTo(diContainer.GetTag<Camera>());
            iconRenderer.Material.World.Ref = Matrix4x4.Identity;
            iconRenderer.Material.MainTexture.Texture = iconFont.Texture;
            iconRenderer.Material.MainSampler.Sampler = iconFont.Sampler;
            HandleResize();
        }
        public void SyncWithScene()
        {
            //Console.WriteLine("bruh");
            foreach(var trigger in triggers)
                trigger.SyncWithScene();
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
            triggers = [];
            if (editor.scene == null)
                return;

            triggers = editor.scene.triggers.Select((t, i) => new Trigger(t, i)).ToArray();

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
            iconRenderer.Icons = triggers.Select(GetDebugIconFor).ToArray();
            editor.fbArea.IsDirty = true;
        }

        private DebugIcon GetDebugIconFor(Trigger trigger)
        {
            var glyph = iconFont.Glyphs[Icons.GetValueOrDefault(trigger.SceneTrigger.type, ForkAwesome.Bell)!];
            return new DebugIcon
            {
                pos = trigger.Location.GlobalPosition,
                uvPos = glyph.Min,
                uvSize = glyph.Size,
                size = Vector2.One * iconSize,
                color = editor.Selected == trigger ? SelectedColor : NormalColor,
                textureWeight = 1f
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

        public IEnumerator<ISelectable> GetEnumerator() => ((IEnumerable<ISelectable>)triggers).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static readonly IReadOnlyDictionary<TriggerType, string> Icons = new Dictionary<TriggerType, string>()
        {
            { TriggerType.Doorway, ForkAwesome.SignIn },
            { TriggerType.SingleplayerStartpoint, ForkAwesome.User },
            { TriggerType.MultiplayerStartpoint, ForkAwesome.Users },
            { TriggerType.NpcStartpoint, ForkAwesome.UserSecret },
            { TriggerType.CameraPosition, ForkAwesome.VideoCamera },
            { TriggerType.Waypoint, ForkAwesome.MapMarker },
            { TriggerType.StartDuel, ForkAwesome.ExclamationTriangle },
            { TriggerType.LeaveDuel, ForkAwesome.ExclamationTriangle },
            { TriggerType.NpcAttackPosition, ForkAwesome.ExclamationTriangle },
            { TriggerType.FlyArea, ForkAwesome.Plane },
            { TriggerType.KillPlayer, ForkAwesome.Hackaday },
            { TriggerType.SetCameraView, ForkAwesome.VideoCamera },
            { TriggerType.SavePoint, ForkAwesome.FloppyO },
            { TriggerType.SwampMarker, ForkAwesome.Tint },
            { TriggerType.RiverMarker, ForkAwesome.Tint },
            { TriggerType.PlayVideo, ForkAwesome.Film },
            { TriggerType.Elevator, ForkAwesome.CaretSquareOUp },
            { TriggerType.GettingACard, ForkAwesome.IdCardO },
            { TriggerType.Sign, ForkAwesome.MapSigns },
            { TriggerType.GettingPixie, ForkAwesome.Paw },
            { TriggerType.UsingPipe, ForkAwesome.Magnet },
            { TriggerType.LeaveDancePlatform, ForkAwesome.Music },
            { TriggerType.RemoveStoneBlocker, ForkAwesome.HandPaperO },
            { TriggerType.RemovePlantBlocker, ForkAwesome.HandPaperO },
            { TriggerType.EventCamera, ForkAwesome.VideoCamera },
            { TriggerType.Platform, ForkAwesome.StreetView },
            { TriggerType.CreatePlatforms, ForkAwesome.Magic },
            { TriggerType.ShadowLight, ForkAwesome.LightbulbO },
            { TriggerType.CreateItems, ForkAwesome.Magic },
            { TriggerType.Item, ForkAwesome.IdCardO },
            { TriggerType.Shrink, ForkAwesome.Compress },
            { TriggerType.WizformMarker, ForkAwesome.ExclamationTriangle },
            { TriggerType.IndoorCamera, ForkAwesome.VideoCamera },
            { TriggerType.UnusedCameraTrigger, ForkAwesome.SunO },
            { TriggerType.LensFlare, ForkAwesome.Cloud },
            { TriggerType.FogModifier, ForkAwesome.Magic },
            { TriggerType.RuneTarget, ForkAwesome.CaretSquareODown },
            { TriggerType.Animal, ForkAwesome.Paw },
            { TriggerType.AnimalWaypoint, ForkAwesome.MapMarker },
            { TriggerType.SceneOpening, ForkAwesome.SignOut },
            { TriggerType.CollectionWizform, ForkAwesome.Paw },
            { TriggerType.ElementalLock, ForkAwesome.Lock },
            { TriggerType.ItemGenerator, ForkAwesome.Magic },
            { TriggerType.Escape, ForkAwesome.SignOut },
            { TriggerType.Jumper, ForkAwesome.Plane },
            { TriggerType.RefreshMana, ForkAwesome.Heartbeat },
            { TriggerType.TemporaryNpc, ForkAwesome.UserSecret },
            { TriggerType.EffectBeam, ForkAwesome.Bolt },
            { TriggerType.MultiplayerObserverPosition, ForkAwesome.VideoCamera },
            { TriggerType.MultiplayerHealingPool, ForkAwesome.Heart },
            { TriggerType.MultiplayerManaPool, ForkAwesome.Heartbeat },
            { TriggerType.Ceiling, ForkAwesome.ArrowDown },
            { TriggerType.HealAllWizforms, ForkAwesome.Heart }
        };
    }
}
