namespace zzre.game.systems;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using zzio;

public partial class BehaviourCollectable : AEntitySetSystem<float>
{
    private const float AliveRotationSpeed = 50f;
    private const float DyingRotationSpeed = 50f * -4f;
    private const float BounceSpeed = 0.01f;
    private const float BounceAmplitude = 0.15f;
    private const float BounceOffset = 1.5f;
    private const float MinLerpDistSqr = 0.1f * 0.1f;
    private const float LerpAmount = 0.3f;
    private const float MaxPlayerDistanceSqr = 1.21f;
    private const float MaxDyingAge = 1.5f;
    private const int MinGoldAmount = 3;
    private const int MaxGoldAmount = 10; // exclusive
    private const string ModelNameXXX = "itmxxx.dff";
    private const string ModelNameYYY = "itmyyy.dff";
    private const string ModelNameZZZ = "itmzzz.dff";

    private Location playerLocation => playerLocationLazy.Value;
    private readonly Game game;
    private readonly Lazy<Location> playerLocationLazy;
    private readonly zzio.db.MappedDB mappedDb;

    public BehaviourCollectable(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        game = diContainer.GetTag<Game>();
        playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
        mappedDb = diContainer.GetTag<zzio.db.MappedDB>();
    }

    [Update]
    private void Update(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        Location location,
        in resources.ClumpInfo clumpInfo,
        ref components.behaviour.Collectable collectable)
    {
        CheckCollection(location, clumpInfo, ref collectable);

        Aging(elapsedTime, entity, ref collectable);
        DyingMovement(location, collectable);
        Rotation(elapsedTime, location, collectable);
    }

    private void Aging(
        float elapsedTime,
        in DefaultEcs.Entity entity,
        ref components.behaviour.Collectable collectable)
    {
        if (!collectable.IsDying)
            return;
        collectable.Age += elapsedTime;
        if (collectable.Age > MaxDyingAge)
            entity.Set<components.Dead>();
    }

    private void DyingMovement(
        Location location,
        in components.behaviour.Collectable collectable)
    {
        if (!collectable.IsDying)
            return;
        var targetPos = playerLocation.LocalPosition + Vector3.UnitY *
            (MathF.Sin(collectable.Age * BounceSpeed) * BounceAmplitude + BounceOffset);

        location.LocalPosition = Vector3.DistanceSquared(targetPos, location.LocalPosition) >= MinLerpDistSqr
            ? Vector3.Lerp(location.LocalPosition, playerLocation.LocalPosition, LerpAmount)
            : targetPos; // TODO: Fix FPS dep. in collectable lerp
    }

    private void Rotation(
        float elapsedTime,
        Location location,
        in components.behaviour.Collectable collectable)
    {
        location.LocalRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY,
            elapsedTime * MathEx.DegToRad * (collectable.IsDying ? DyingRotationSpeed : AliveRotationSpeed));
    }

    private void CheckCollection(
        Location location,
        in resources.ClumpInfo clumpInfo,
        ref components.behaviour.Collectable collectable)
    {
        if (collectable.IsDying || location.DistanceSquared(playerLocation) >= MaxPlayerDistanceSqr)
            return;

        collectable.IsDying = true;
        collectable.Age = 0f;
        // TODO: Play soundeffect on collectable grab
        if (!collectable.IsDynamic)
            game.Publish(new GSModRemoveItem(collectable.ModelId));
        Collect(clumpInfo);
    }

    private void Collect(in resources.ClumpInfo clumpInfo)
    {
        var random = Random.Shared;
        int parsedItemId = -1;
        int itemId = clumpInfo.Name.ToLowerInvariant() switch
        {
            ModelNameXXX => (int)random.NextOf(ItemPoolXXX),
            ModelNameYYY => (int)random.NextOf(ItemPoolYYY),
            ModelNameZZZ => (int)random.NextOf(ItemPoolZZZ),
            _ when clumpInfo.Name.Length > 6 && int.TryParse(clumpInfo.Name.Substring(3, 3), out parsedItemId) => parsedItemId,
            _ => throw new InvalidOperationException($"Invalid collectable model name: {clumpInfo.Name}")
        };

        int amount = itemId == (int)StdItemId.Gold
            ? random.Next(MinGoldAmount, MaxGoldAmount)
            : 1;
        game.PlayerEntity.Get<Inventory>().AddItem(itemId, amount);

        var dbRow = mappedDb.Items.First(i => i.CardId.EntityId == itemId);
        if (dbRow.Special != 0 || parsedItemId < 0)
            game.Publish(new messages.GotCard(dbRow.Uid, amount));
    }

    private static readonly ImmutableArray<StdItemId> ItemPoolXXX = new[]
    {
        Enumerable.Repeat(StdItemId.Gold, 8),
        Enumerable.Repeat(StdItemId.SmallHealingPotion, 6),
        Enumerable.Repeat(StdItemId.NormalHealingPotion, 3),
        Enumerable.Repeat(StdItemId.BigHealingPotion, 2),
        Enumerable.Repeat(StdItemId.HealingHerb, 1)
    }.SelectMany().ToImmutableArray();

    private static readonly ImmutableArray<StdItemId> ItemPoolYYY = new[]
    {
        StdItemId.SmallHealingPotion,
        StdItemId.SmallHealingPotion,
        StdItemId.SmallHealingPotion,
        StdItemId.SmallHealingPotion,
        StdItemId.NormalHealingPotion,
        StdItemId.BigHealingPotion,
        StdItemId.HealingHerb,
        StdItemId.ManaPotion,
        StdItemId.GarlicDispenser,
        StdItemId.GarlicDispenser
    }.ToImmutableArray();

    private static readonly ImmutableArray<StdItemId> ItemPoolZZZ = new[]
    {
        StdItemId.BigHealingPotion,
        StdItemId.BigHealingPotion,
        StdItemId.HealingHerb,
        StdItemId.ManaPotion,
        StdItemId.GarlicDispenser,
        StdItemId.GoldenCarrot
    }.ToImmutableArray();
}
