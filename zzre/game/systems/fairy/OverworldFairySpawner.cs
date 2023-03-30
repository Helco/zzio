using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.Resource;
using DefaultEcs.System;

namespace zzre.game.systems;

public partial class OverworldFairySpawner : AEntitySetSystem<float>
{
    private readonly zzio.db.MappedDB db;
    private readonly IDisposable sceneChangingSubscription;
    private readonly IDisposable inventoryAddedSubscription;

    public OverworldFairySpawner(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        db = diContainer.GetTag<zzio.db.MappedDB>();
        sceneChangingSubscription = World.Subscribe<messages.SceneChanging>(HandleSceneChanging);
        inventoryAddedSubscription = World.SubscribeComponentAdded<Inventory>(HandleInventoryAdded);
    }

    public override void Dispose()
    {
        base.Dispose();
        inventoryAddedSubscription.Dispose();
    }

    private void HandleSceneChanging(in messages.SceneChanging _) => World
        .GetEntities()
        .With<components.FairyHoverBehind>()
        .DisposeAll();

    private void HandleInventoryAdded(in DefaultEcs.Entity entity, in Inventory value)
    {
        entity.Set(new components.SpawnedFairy());
        Update(entity, value, ref entity.Get<components.SpawnedFairy>()); // no need to wait for next frame
    }

    [Update]
    private void Update(
        DefaultEcs.Entity entity,
        in Inventory inventory,
        ref components.SpawnedFairy spawnedFairy)
    {
        var intendedFairy = inventory.ActiveOverworldFairy;
        var actualFairy = spawnedFairy.Entity.IsAlive && spawnedFairy.Entity.Has<zzio.InventoryFairy>()
            ? spawnedFairy.Entity.Get<zzio.InventoryFairy>()
            : null;
        if (actualFairy == intendedFairy)
            return;
        if (spawnedFairy.Entity.IsAlive)
            spawnedFairy.Entity.Dispose();
        spawnedFairy.Entity = default;
        if (intendedFairy == null)
            return;

        SpawnFairy(entity, intendedFairy);
    }

    private void SpawnFairy(DefaultEcs.Entity parent, zzio.InventoryFairy invFairy)
    {
        var dbRow = db.GetFairy(invFairy.dbUID);
        var fairy = World.CreateEntity();
        parent.Set(new components.SpawnedFairy(fairy));
        fairy.Set(new components.Parent(parent));
        fairy.Set(new Location()
        {
            Parent = World.Get<Location>(),
            LocalScale = Vector3.One * 0.4f
        });
        fairy.Set(invFairy);
        fairy.Set(dbRow);
        fairy.Set(ManagedResource<zzio.ActorExDescription>.Create(dbRow.Mesh));
        fairy.Set(components.FairyHoverBehind.Normal);
        fairy.Set<components.Velocity>();
        fairy.Set(new components.FairyAnimation()
        {
            TargetDirection = Vector3.UnitX, // does not usually affects overworld fairies
            Current = zzio.AnimationType.PixieFlounder // something not used by fairies
        });

        var actorParts = fairy.Get<components.ActorParts>();
        actorParts.Body.Get<Location>().Parent = fairy.Get<Location>();
        //actorParts.Body.Get<Skeleton>().JumpToAnimation(actorParts.Body.Get<components.AnimationPool>()[zzio.AnimationType.SpecialIdle0]);
        actorParts.Wings!.Value.Get<Skeleton>().JumpToAnimation(actorParts.Wings.Value.Get<components.AnimationPool>()[zzio.AnimationType.Idle0]);
    }
}
