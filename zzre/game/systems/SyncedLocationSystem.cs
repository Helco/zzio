using System;
using DefaultEcs.System;
using Veldrid;
using zzre.rendering;

namespace zzre.game
{
    public class SyncedLocationSystem : BaseDisposable, ISystem<float>
    {
        private readonly LocationBuffer locationBuffer;
        private readonly IDisposable renderSubscription;

        public bool IsEnabled
        {
            get => true;
            set => throw new InvalidOperationException();
        }

        public SyncedLocationSystem(ITagContainer diContainer)
        {
            diContainer.AddTag(this);
            locationBuffer = diContainer.GetTag<LocationBuffer>();
            var ecsWorld = diContainer.GetTag<DefaultEcs.World>();
            renderSubscription = ecsWorld.Subscribe<messages.Render>(Render);
        }

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            renderSubscription.Dispose();
        }

        private void Render(in messages.Render message) => locationBuffer.Update(message.CommandList);

        public void Update(float state) { }

        public Location AddTo(DefaultEcs.Entity entity)
        {
            Location location;
            if (entity.Has<Location>())
                location = entity.Get<Location>();
            else
            {
                location = new Location();
                entity.Set(location);
            }

            if (!entity.Has<components.SyncedLocation>())
            {
                var range = locationBuffer.Add(location);
                entity.Set(new components.SyncedLocation(range));
            }

            return location;
        }
    }
}
