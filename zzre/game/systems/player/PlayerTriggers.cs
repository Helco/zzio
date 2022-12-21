using System;
using System.Collections.Generic;
using System.Numerics;
using DefaultEcs.Resource;
using DefaultEcs.System;

namespace zzre.game.systems;

[PauseDuring(PauseTrigger.UIScreen)]
public class PlayerTriggers : ISystem<float>
{
    private const Veldrid.MouseButton TriggerButton = Veldrid.MouseButton.Left;
    private const float MaxNpcDirDistance = 0.4f;
    private const float NpcMarkerDistance = 0.3f;

    private readonly DefaultEcs.World world;
    private readonly IZanzarahContainer zzContainer;
    private readonly PlayerControls playerControls;
    private readonly Location cameraLocation;
    private readonly IDisposable sceneChangingDisposable;
    private DefaultEcs.Entity npcMarker;

    public bool IsEnabled { get; set; } = true;
    public bool IsMarkerActive => npcMarker.Has<components.Visibility>();

    private Location playerLocation => playerLocationLazy.Value;
    private readonly Lazy<Location> playerLocationLazy;

    public PlayerTriggers(ITagContainer diContainer)
    {
        world = diContainer.GetTag<DefaultEcs.World>();
        playerControls = diContainer.GetTag<PlayerControls>();
        zzContainer = diContainer.GetTag<IZanzarahContainer>();
        zzContainer.OnMouseDown += HandleMouseDown;

        var game = diContainer.GetTag<Game>();
        cameraLocation = diContainer.GetTag<rendering.Camera>().Location;
        playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());

        sceneChangingDisposable = world.Subscribe<messages.SceneChanging>(HandleSceneChanging);
    }

    public void Dispose()
    {
        zzContainer.OnMouseDown -= HandleMouseDown;
        sceneChangingDisposable.Dispose();
    }

    private void HandleSceneChanging(in messages.SceneChanging _)
    {
        if (npcMarker.IsAlive)
            npcMarker.Dispose();
        npcMarker = default;
    }

    private void HandleMouseDown(Veldrid.MouseButton button, Vector2 _)
    {
        if (!IsEnabled || !IsMarkerActive || button != TriggerButton)
            return;

        var activeNpc = world.Get<components.ActiveNPC>().Entity;
        world.Publish(new messages.StartDialog(activeNpc, DialogCause.Trigger));
    }

    public void Update(float deltaTime)
    {
        // TODO: Add sound effect for looking at triggers

        EnsureNpcMarker();
        if (TryGetTriggerableNPC(out var npc))
        {
            // TODO: Fix marker distance for flying-type NPCs
            npcMarker.Set(components.Visibility.Visible);
            var npcMarkerLoc = npcMarker.Get<Location>();
            npcMarkerLoc.LocalPosition =
                npc.Get<Location>().LocalPosition +
                Vector3.UnitY * (npc.Get<Sphere>().Radius * 0.5f + NpcMarkerDistance);
            return;
        }
        npcMarker.Remove<components.Visibility>();

        // TODO: Add attack item triggers
        // TODO: Add look at sign triggers
    }

    private bool TryGetTriggerableNPC(out DefaultEcs.Entity npc)
    {
        npc = default;
        if (playerControls.IsLocked || !world.Has<components.ActiveNPC>())
            return false;

        npc = world.Get<components.ActiveNPC>().Entity;
        var npcDbRow = npc.Get<zzio.db.NpcRow>();
        if (string.IsNullOrWhiteSpace(npcDbRow.TriggerScript))
            return false;

        var npcLocation = npc.Get<Location>();
        var playerToNpc = (playerLocation.GlobalPosition - npcLocation.GlobalPosition) with { Y = 0.001f };
        var cameraDir = cameraLocation.GlobalForward with { Y = 0.001f };
        var dirDistance = Vector3.Distance(Vector3.Normalize(playerToNpc), Vector3.Normalize(cameraDir));
        return dirDistance < MaxNpcDirDistance;
    }

    private void EnsureNpcMarker()
    {
        if (npcMarker.IsAlive)
            return;
        npcMarker = world.CreateEntity();
        npcMarker.Set(new Location());
        npcMarker.Set(new components.behaviour.Rotate(Vector3.UnitY, 90f));
        npcMarker.Set(ManagedResource<ClumpBuffers>.Create(
            new resources.ClumpInfo(resources.ClumpType.Model, "marker.dff")));
        ModelLoader.LoadMaterialsFor(npcMarker,
            zzio.scn.FOModelRenderType.Solid,
            zzio.IColor.Green,
            new zzio.SurfaceProperties(1f, 1f, 1f));
        var materials = npcMarker.Get<List<materials.BaseModelInstancedMaterial>>();
        foreach (var material in materials)
            material.Uniforms.Ref.vertexColorFactor = 1f;
    }
}
