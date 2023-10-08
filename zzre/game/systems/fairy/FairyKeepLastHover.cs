namespace zzre.game.systems;
using System;
using System.Numerics;
using DefaultEcs.System;

public partial class FairyKeepLastHover : AEntitySetSystem<float>
{
    private const float HoverDistance = -0.7f;
    private const float HoverRadius = 0.2f;
    private const float BounceMagnitude = 0.05f;
    private readonly GameTime time;

    public FairyKeepLastHover(ITagContainer diContainer)
        : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        time = diContainer.GetTag<GameTime>();
        Set.EntityAdded += HandleAdded;
    }

    private void HandleAdded(in DefaultEcs.Entity entity)
    {
        var location = entity.Get<Location>();
        entity.Set(new components.Velocity(new(0f, 0.01f, 0f)));
        entity.Set(new components.FairyHoverStart(location.LocalPosition));
        // ^ this is not modifying the set as fairies should already have this component
    }

    [WithPredicate]
    private bool IsInState(in components.FairyHoverState state) => state == components.FairyHoverState.KeepLastHover;

    [Update]
    private void Update(
        float elapsedTime,
        in components.Parent parent,
        in components.FairyHoverStart hoverStart,
        ref components.FairyHoverOffset hoverOffset,
        Location location)
    {
        // similar but not equal to the FairyHoverOffset update
        float sinDelta = MathF.Sin(elapsedTime), cosDelta = MathF.Cos(elapsedTime);
        var offset = hoverOffset.Value;
        var oldOffsetX = offset.X;
        offset.X = cosDelta * oldOffsetX - sinDelta * offset.Z;
        offset.Y = 0f;
        offset.Z = cosDelta * offset.Z + sinDelta * oldOffsetX;
        if (MathEx.CmpZero(offset.X))
            offset.X = 0.1f;
        if (MathEx.CmpZero(offset.Z))
            offset.Z = 0.1f;
        offset = Vector3.Normalize(offset) * HoverRadius;
        offset.Y = MathF.Cos(time.TotalElapsed) * BounceMagnitude;
        location.LocalPosition = hoverStart.Value + offset;
        hoverOffset = new(offset);

        var parentDirection = parent.Entity.Get<Location>().InnerForward;
        location.LookIn(parentDirection with { Y = 0f });
        location.LocalPosition += location.InnerForward * HoverDistance;
    }
}
