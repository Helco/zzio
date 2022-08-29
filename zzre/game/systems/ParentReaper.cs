namespace zzre.game.systems;
using System;
using DefaultEcs.System;

public partial class ParentReaper : AEntitySetSystem<float>
{
    private readonly IDisposable sceneChangingSubscription;

    public ParentReaper(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
    {
        sceneChangingSubscription = World.Subscribe<messages.SceneChanging>(HandleSceneChanging);
    }

    public override void Dispose()
    {
        base.Dispose();
        sceneChangingSubscription.Dispose();
    }

    private void HandleSceneChanging(in messages.SceneChanging _) => Update(0f);

    [Update]
    private void Update(in DefaultEcs.Entity entity, in components.Parent parent)
    {
        if (!parent.Entity.IsAlive)
            entity.Dispose();
    }
}
