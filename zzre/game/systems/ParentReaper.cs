namespace zzre.game.systems;
using System;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;

public partial class ParentReaper : ISystem<float>
{
    private readonly IDisposable sceneChangingSubscription;
    private readonly DefaultEcs.EntityMultiMap<components.Parent> set;
    private readonly List<DefaultEcs.Entity> toBeDeleted = new(32);

    public bool IsEnabled { get; set; } = true;

    public ParentReaper(ITagContainer diContainer)
    {
        var world = diContainer.GetTag<DefaultEcs.World>();
        sceneChangingSubscription = world.Subscribe<messages.SceneChanging>(HandleSceneChanging);
        set = world.GetEntities().AsMultiMap<components.Parent>(32);
    }

    public void Dispose()
    {
        set.Dispose();
        sceneChangingSubscription.Dispose();
    }

    private void HandleSceneChanging(in messages.SceneChanging _) => Update(0f);

    public void Update(float state)
    {
        do
        {
            toBeDeleted.Clear();
            foreach (var key in set.Keys.Where(k => !k.Entity.IsAlive))
                toBeDeleted.AddRange(set[key]);

            foreach (var child in toBeDeleted)
                child.Dispose();
        } while (toBeDeleted.Any()) ;
    }
}
