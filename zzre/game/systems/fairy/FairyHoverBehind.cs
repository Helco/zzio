namespace zzre.game.systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using Mode = zzre.game.components.FairyHoverBehind.Mode;

public partial class FairyHoverBehind : AEntitySetSystem<float>
{
    private readonly Random random = GlobalRandom.Get;
    private readonly GameTime time;
    private readonly IDisposable playerEnteredSubscription;
    private readonly IDisposable addedSubscription;
    private WorldCollider? worldCollider;

    public FairyHoverBehind(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        time = diContainer.GetTag<GameTime>();
        playerEnteredSubscription = World.Subscribe<messages.PlayerEntered>(HandlePlayerEntered);
        addedSubscription = World.SubscribeComponentAdded<components.FairyHoverBehind>(HandleAddedComponent);
    }

    public override void Dispose()
    {
        base.Dispose();
        playerEnteredSubscription.Dispose();
        addedSubscription.Dispose();
    }

    private void HandlePlayerEntered(in messages.PlayerEntered _)
    {
        foreach (var entity in Set.GetEntities())
            ResetPosition(entity);
    }

    private void HandleAddedComponent(in DefaultEcs.Entity entity, in components.FairyHoverBehind value) =>
        ResetPosition(entity);

    private void ResetPosition(in DefaultEcs.Entity entity)
    {
        var parent = entity.Get<components.Parent>().Entity;
        entity.Get<Location>().LocalPosition = parent.Get<Location>().LocalPosition;
    }

    [Update]
    private void Update(
        float elapsedTime,
        DefaultEcs.Entity entity,
        Location location,
        in components.Parent parent,
        ref components.FairyHoverBehind hoverBehind,
        ref components.Velocity velocity)
    {
        hoverBehind.TimeLeft -= elapsedTime;
        if (hoverBehind.TimeLeft <= 0f)
        {
            hoverBehind.CurMode = random.NextOf<Mode>();
            hoverBehind.TimeLeft = (random.NextFloat() + 1) * hoverBehind.MaxDuration / 2f;
        }

        var parentPos = parent.Entity.Get<Location>().LocalPosition;
        var parentDir = parent.Entity.Get<components.PuppetActorMovement>().TargetDirection;
        var parentRight = new Vector3(-parentDir.Z, 0f, parentDir.X) / 2f;

        var targetOffset = parentDir * -0.8f * hoverBehind.Distance;
        targetOffset = hoverBehind.CurMode switch
        {
            Mode.CenterHigh => targetOffset with { Y = hoverBehind.Distance * 1.2f },
            Mode.LeftLow => (targetOffset - parentRight) with { Y = hoverBehind.Distance * 0.8f },
            Mode.LeftHigh => (targetOffset - parentRight) with { Y = hoverBehind.Distance },
            Mode.RightHigh => (targetOffset + parentRight) with { Y = hoverBehind.Distance * 1.1f },
            Mode.RightLow => (targetOffset + parentRight) with { Y = hoverBehind.Distance * 0.8f },
            Mode.CenterLow => targetOffset with { Y = hoverBehind.Distance },
            _ => throw new NotImplementedException($"Unimplemented fairy hover behind mode {hoverBehind.CurMode}")
        };
        // TODO: Add water handling to fairy hover behaviour

        if (entity.Has<components.FairyLookAt>())
            location.LookAt(entity.Get<components.FairyLookAt>().Target);
        else
            location.LookIn(parentDir);

        var targetPosition = parentPos + targetOffset;
        var move = targetPosition - location.LocalPosition;
        move -= move * MathF.Pow(0.1f, elapsedTime);
        location.LocalPosition += move + GetHoverOffset(ref hoverBehind, elapsedTime);
        var minYPos = parentPos.Y - 0.5f;
        if (location.LocalPosition.Y < minYPos)
            location.LocalPosition = location.LocalPosition with { Y = minYPos };
        velocity = new(move);

        worldCollider ??= World.Get<WorldCollider>();
        if (worldCollider.Intersects(new Sphere(location.LocalPosition, 0.6f)))
        {
            hoverBehind.TimeLeft = 3f;
            hoverBehind.CurMode = Mode.CenterHigh;
        }
    }

    private Vector3 GetHoverOffset(ref components.FairyHoverBehind hoverBehind, float elapsedTime)
    {
        var offset = hoverBehind.HoverOffset;
        if (offset == Vector3.Zero)
        {
            offset = new(
                random.InLine() * 10f,
                0f,
                random.InLine() * 10f);
        }

        float sinDelta = MathF.Sin(elapsedTime), cosDelta = MathF.Cos(elapsedTime);
        offset.X = cosDelta * offset.X - sinDelta * offset.Z;
        offset.Y = 0.01f;
        offset.Z = sinDelta * offset.X + cosDelta * offset.Z;
        offset = Vector3.Normalize(offset) * elapsedTime * 0.4f; // yes, there is basically no non-vertical movement
        offset.Y = MathF.Cos(time.TotalElapsed / 100f) * (elapsedTime * 0.3f); // TODO: Fix framerate-dependent vertical fairy hovering

        return hoverBehind.HoverOffset = offset;
    }
}
