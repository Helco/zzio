using System;
using DefaultEcs.System;
using Veldrid;
using zzio;
using zzre.rendering;

namespace zzre.game.systems;

public class SyncedLocation : BaseDisposable, ISystem<CommandList>
{
    private readonly LocationBuffer locationBuffer;
    private readonly IDisposable addSubscription;
    private readonly IDisposable removeSubscription;

    public bool IsEnabled
    {
        get => true;
        set => throw new InvalidOperationException();
    }

    public SyncedLocation(ITagContainer diContainer)
    {
        diContainer.AddTag(this);
        locationBuffer = diContainer.GetTag<LocationBuffer>();
        var ecsWorld = diContainer.GetTag<DefaultEcs.World>();
        addSubscription = ecsWorld.SubscribeComponentAdded<components.SyncedLocation>(HandleAddedComponent);
        removeSubscription = ecsWorld.SubscribeComponentRemoved<components.SyncedLocation>(HandleRemovedComponent);
    }

    protected override void DisposeManaged()
    {
        base.DisposeManaged();
        addSubscription.Dispose();
        removeSubscription.Dispose();
    }

    public void Update(CommandList cl) => locationBuffer.Update(cl);

    private void HandleAddedComponent(in DefaultEcs.Entity entity, in components.SyncedLocation value)
    {
        if (value.BufferRange.Buffer != null)
            return;

        Location location;
        if (entity.Has<Location>())
            location = entity.Get<Location>();
        else
            entity.Set(location = new Location());

        entity.Set(new components.SyncedLocation(locationBuffer.Add(location)));
    }

    private void HandleRemovedComponent(in DefaultEcs.Entity entity, in components.SyncedLocation value)
    {
        if (value.BufferRange.Buffer == null)
            return;
        locationBuffer.Remove(value.BufferRange);
    }
}
