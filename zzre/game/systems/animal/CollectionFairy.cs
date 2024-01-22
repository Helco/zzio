using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.Resource;
using DefaultEcs.System;
using zzio;
using zzio.db;
using zzio.scn;

namespace zzre.game.systems;

public partial class CollectionFairy : AEntitySetSystem<float>
{
    private const float HorizontalFrequency = 1f / 300f;
    private static readonly Vector3 BounceAmplitude = new(0.1f, 0.02f, 0.1f);
    private const int HighFairyIdx = 40;

    private readonly MappedDB db;
    private readonly Game game;
    private readonly GameTime time;
    private readonly IDisposable sceneLoadSubscription;
    private readonly IDisposable sceneChangingSubscription;

    public CollectionFairy(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        db = diContainer.GetTag<MappedDB>();
        game = diContainer.GetTag<Game>();
        time = diContainer.GetTag<GameTime>();
        sceneLoadSubscription = World.Subscribe<messages.SceneLoaded>(HandleSceneLoaded);
        sceneChangingSubscription = World.Subscribe<messages.SceneChanging>(HandleSceneChanging);
    }

    public override void Dispose()
    {
        base.Dispose();
        sceneLoadSubscription.Dispose();
        sceneChangingSubscription.Dispose();
    }

    [Update]
    public void Update(
        in DefaultEcs.Entity entity,
        Location location,
        in components.ActorParts parts,
        in components.CollectionFairy collFairy,
        in InventoryFairy invFairy)
    {
        if (invFairy.isInUse)
        {
            entity.Set<components.Dead>();
            return;
        }

        var bounce = new Vector3(
            MathF.Cos(time.TotalElapsed * 1000f * HorizontalFrequency),
            MathF.Cos(time.TotalElapsed * 1000f / collFairy.VerticalFrequency),
            MathF.Sin(time.TotalElapsed * 1000f * HorizontalFrequency)) * BounceAmplitude;
        var playerPos = game.PlayerEntity.Get<Location>().GlobalPosition;
        var bodyLocation = parts.Body.Get<Location>();
        playerPos.Y = bodyLocation.GlobalPosition.Y;
        var backOff = MathEx.SafeNormalize(playerPos - bodyLocation.GlobalPosition);
        bodyLocation.LocalPosition = bounce - backOff;
        bodyLocation.LookAt(playerPos);
    }

    private void HandleSceneChanging(in messages.SceneChanging _) => Set.DisposeAll();

    private void HandleSceneLoaded(in messages.SceneLoaded message)
    {
        if (!IsEnabled || !message.Scene.dataset.canChangeDeck)
            return;

        var potentialSpawns = message.Scene.triggers
            .Where(t => t.type == TriggerType.CollectionWizform)
            .ToList();
        var unusedFairies = game.PlayerEntity.Get<Inventory>().Fairies.Where(f => !f.isInUse);
        var random = Random.Shared;

        foreach (var invFairy in unusedFairies)
        {
            if (!potentialSpawns.Any())
                break;
            var isHighIdx = invFairy.cardId.EntityId > HighFairyIdx ? 1u : 0;
            var spawnI = random.Next(potentialSpawns.Count);
            if (isHighIdx != potentialSpawns[spawnI].ii1)
                continue;

            var dbRow = db.GetFairy(invFairy.dbUID);
            var trigger = potentialSpawns[spawnI];
            potentialSpawns.RemoveAt(spawnI);
            var entity = World.CreateEntity();
            entity.Set(new Location()
            {
                LocalPosition = trigger.pos
            });
            entity.Set(invFairy);
            entity.Set(ManagedResource<ActorExDescription>.Create(dbRow.Mesh));
            entity.Set(components.CollectionFairy.Random);

            var actorParts = entity.Get<components.ActorParts>();
            actorParts.Body.Get<Location>().Parent = entity.Get<Location>();
            var bodyAniPool = actorParts.Body.Get<components.AnimationPool>();
            actorParts.Body.Get<Skeleton>().JumpToAnimation(bodyAniPool.Contains(AnimationType.SpecialIdle0)
                ? bodyAniPool[AnimationType.SpecialIdle0]
                : bodyAniPool[AnimationType.Idle0]);
            actorParts.Wings!.Value.Get<Skeleton>().JumpToAnimation(
                actorParts.Wings.Value.Get<components.AnimationPool>()[AnimationType.Idle0]);
        }
    }
}
