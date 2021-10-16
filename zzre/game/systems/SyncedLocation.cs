using System;
using DefaultEcs.System;
using Veldrid;
using zzre.rendering;

namespace zzre.game.systems
{
    public class SyncedLocation : BaseDisposable, ISystem<float>
    {
        private readonly LocationBuffer locationBuffer;
        private readonly IDisposable renderSubscription;
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
            renderSubscription = ecsWorld.Subscribe<messages.Render>(Render);
            addSubscription = ecsWorld.SubscribeComponentAdded<components.SyncedLocation>(HandleAddedComponent);
            removeSubscription = ecsWorld.SubscribeComponentRemoved<components.SyncedLocation>(HandleRemovedComponent);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            renderSubscription.Dispose();
            addSubscription.Dispose();
            removeSubscription.Dispose();
        }

        private void Render(in messages.Render message) => locationBuffer.Update(message.CommandList);

        public void Update(float state) { }

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
}
