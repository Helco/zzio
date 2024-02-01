using System;
using DefaultEcs.System;
using zzre.materials;

namespace zzre.game.systems.effect;

public abstract partial class BaseCombinerPart<TData, TState> : AEntityMultiMapSystem<float, components.Parent>
    where TData : zzio.effect.IEffectPart
    where TState : struct
{
    protected readonly EffectMesh effectMesh;
    private readonly IDisposable addDisposable;
    private readonly IDisposable removeDisposable;

    public BaseCombinerPart(ITagContainer diContainer) :
        base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
    {
        effectMesh = diContainer.GetTag<EffectMesh>();
        addDisposable = World.SubscribeEntityComponentAdded<TData>(HandleAddedComponent);
        removeDisposable = World.SubscribeEntityComponentRemoved<TState>(HandleRemovedComponent);
    }

    public override void Dispose()
    {
        base.Dispose();
        addDisposable.Dispose();
        removeDisposable.Dispose();
    }

    protected abstract void HandleRemovedComponent(in DefaultEcs.Entity entity, in TState state);

    protected abstract void HandleAddedComponent(in DefaultEcs.Entity entity, in TData data);

    [Update]
    protected abstract void Update(
        float elapsedTime,
        in components.Parent parent,
        ref TState state,
        in TData data,
        ref components.effect.RenderIndices indices);
}
