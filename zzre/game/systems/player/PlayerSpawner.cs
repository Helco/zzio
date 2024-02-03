namespace zzre.game.systems;
using System;
using System.Numerics;
using DefaultEcs.System;
using zzio.scn;
using zzre.rendering;

public class PlayerSpawner : ISystem<float>
{
    private const float ControlsLockedInterior = 1.2f;
    private const float ControlsLockedExterior = 1.5f;
    private const float ControlsLockedRune = 2f;

    private readonly ITagContainer diContainer;
    private readonly zzio.Savegame savegame;
    private readonly DefaultEcs.World ecsWorld;
    private readonly IDisposable sceneChangingSubscription;
    private readonly IDisposable sceneLoadedSubscription;
    private readonly IDisposable playerEnteredSubscription;

    private DefaultEcs.Entity playerEntity;

    public bool IsEnabled { get; set; } = true;

    public PlayerSpawner(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        savegame = diContainer.GetTag<zzio.Savegame>();
        ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        sceneChangingSubscription = ecsWorld.Subscribe<messages.SceneChanging>(HandleSceneChanging);
        sceneLoadedSubscription = ecsWorld.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        playerEnteredSubscription = ecsWorld.Subscribe<messages.PlayerEntered>(HandlePlayerEntered);

        ecsWorld.SetMaxCapacity<components.PlayerEntity>(1);
    }

    public void Dispose()
    {
        sceneChangingSubscription.Dispose();
        sceneLoadedSubscription.Dispose();
        playerEnteredSubscription.Dispose();
    }

    private void HandleSceneChanging(in messages.SceneChanging _) { }

    private void HandleSceneLoaded(in messages.SceneLoaded message)
    {
        if (playerEntity != default)
            return;
        playerEntity = ecsWorld.CreateEntity();
        playerEntity.Set(new Location()
        {
            Parent = ecsWorld.Get<Location>(),
            LocalPosition = new Vector3(216f, 40.5f, 219f)
        });
        playerEntity.Set(DefaultEcs.Resource.ManagedResource<zzio.ActorExDescription>.Create("chr01"));
        playerEntity.Set(components.Visibility.Visible);
        playerEntity.Set<components.PlayerControls>();
        playerEntity.Set<components.PlayerPuppet>();
        playerEntity.Set(components.PhysicParameters.Standard);
        playerEntity.Set(new components.NonFairyAnimation(Random.Shared));
        playerEntity.Set<components.PuppetActorMovement>();
        playerEntity.Set(components.FindActorFloorCollisions.Default);
        playerEntity.Set(components.ActorLighting.Default);
        var playerColliderSize = GetColliderSize(playerEntity);
        playerEntity.Set(new components.HumanPhysics(playerColliderSize));
        playerEntity.Set(new Sphere(Vector3.Zero, playerColliderSize));
        playerEntity.Set(new Inventory(diContainer, savegame));
        playerEntity.Set(components.GameFlow.Normal); // TODO: Move GameFlow component to world instead of player entity
        ecsWorld.Set(new components.PlayerEntity(playerEntity));
    }

    private void HandlePlayerEntered(in messages.PlayerEntered message)
    {
        var playerLocation = playerEntity.Get<Location>();
        var trigger = message.EntryTrigger;
        var startPos = trigger.pos;
        if (trigger.type == TriggerType.Doorway && trigger.colliderType == TriggerColliderType.Sphere)
            startPos += trigger.dir with { Y = 0f } * trigger.radius * 1.2f;
        playerLocation.LocalPosition = startPos;
        playerLocation.LookIn(MathEx.SafeNormalize(trigger.dir with { Y = 0f }));
        playerEntity.Get<components.ActorParts>().Body.Get<Location>().LocalRotation = playerLocation.LocalRotation;
        playerEntity.Get<components.PuppetActorMovement>().TargetDirection = playerLocation.GlobalForward;
        ecsWorld.Publish(new messages.CreaturePlaceToGround(playerEntity));

        var isInterior = ecsWorld.Get<Scene>().dataset.isInterior;
        if (trigger.type == TriggerType.Doorway)
            ecsWorld.Publish(new messages.LockPlayerControl(
                isInterior ? ControlsLockedInterior : ControlsLockedExterior,
                MovingForward: true));
        else
            ecsWorld.Publish(new messages.LockPlayerControl(ControlsLockedRune, MovingForward: false));
    }

    private static float GetColliderSize(DefaultEcs.Entity playerEntity)
    {
        var playerActorParts = playerEntity.Get<components.ActorParts>();
        var playerBodyClump = playerActorParts.Body.Get<ClumpMesh>();
        return playerBodyClump.BoundingBox.Size.Y;
    }

    public void Update(float state) { }
}
