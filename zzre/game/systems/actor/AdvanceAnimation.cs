using DefaultEcs.System;

namespace zzre.game.systems;

public partial class AdvanceAnimation : AEntitySetSystem<float>
{
    public AdvanceAnimation(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
    }

    [Update]
    private static void Update(float elapsedTime, DefaultEcs.Entity entity, ref Skeleton component)
    {
        var optSpeed= entity.TryGet<components.AnimationSpeed>();
        float factor = optSpeed.HasValue ? optSpeed.Value.Factor : 1f;
        component.AddTime(elapsedTime * factor);
    }
}
