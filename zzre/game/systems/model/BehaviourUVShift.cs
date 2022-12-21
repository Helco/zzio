using System;
using DefaultEcs.System;

namespace zzre.game.systems;

public partial class BehaviourUVShift : AEntitySetSystem<float>
{
    private readonly IDisposable addedSubscription;

    public BehaviourUVShift(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        addedSubscription = World.SubscribeComponentAdded<components.behaviour.UVShift>(HandleComponentAdded);
    }

    public override void Dispose()
    {
        base.Dispose();
        addedSubscription?.Dispose();
    }

    private void HandleComponentAdded(in DefaultEcs.Entity entity, in components.behaviour.UVShift _)
    {
        entity.Set(components.TexShift.Default);
    }

    [Update]
    private void Update(float elapsedTime, in components.behaviour.UVShift shift, ref components.TexShift texShift)
    {
        texShift.Matrix.M31 += shift.Shift * elapsedTime;
    }
}
