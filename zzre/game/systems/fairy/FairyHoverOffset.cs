namespace zzre.game.systems;
using System;
using System.Numerics;
using DefaultEcs.System;

public partial class FairyHoverOffset : AEntitySetSystem<float>
{
    private readonly Random random = Random.Shared;
    private readonly GameTime time;

    public FairyHoverOffset(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        time = diContainer.GetTag<GameTime>();
    }

    [WithPredicate]
    private bool IsInRelevantStates(in components.FairyHoverState state) => state == components.FairyHoverState.Behind;

    [Update]
    private void Update(float elapsedTime, ref components.FairyHoverOffset component)
    {
        var offset = component.Value;
        if (offset == Vector3.Zero)
        {
            offset = new(
                random.InLine() * 10f,
                0f,
                random.InLine() * 10f);
        }

        float sinDelta = MathF.Sin(elapsedTime), cosDelta = MathF.Cos(elapsedTime);
        var oldOffsetX = offset.X;
        offset.X = cosDelta * oldOffsetX - sinDelta * offset.Z;
        offset.Y = 0.01f;
        offset.Z = sinDelta * oldOffsetX + cosDelta * offset.Z;
        offset = Vector3.Normalize(offset) * elapsedTime * 0.4f; // yes, there is basically no non-vertical movement
        offset.Y = MathF.Cos(time.TotalElapsed / 100f) * (elapsedTime * 0.3f); // TODO: Fix framerate-dependent vertical fairy hovering
        // ^ this is framerate-dependent as an absolute value depends on a single frame delta

        component = new(offset);
    }
}
