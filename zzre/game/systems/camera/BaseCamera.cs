using System;
using DefaultEcs.System;
using zzre.rendering;

namespace zzre.game.systems
{
    public class BaseCamera : ISystem<float>
    {
        protected readonly IZanzarahContainer zzContainer;
        protected readonly Camera camera;
        protected readonly DefaultEcs.World world;
        private readonly Lazy<Location> playerLocationLazy;
        protected Location playerLocation => playerLocationLazy.Value;

        private bool isEnabled;
        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (world.Has<components.ActiveCamera>())
                {
                    ref readonly var activeCamera = ref world.Get<components.ActiveCamera>();
                    if (activeCamera.System != this)
                        activeCamera.System.isEnabled = false;
                }
                if (value)
                    world.Set(new components.ActiveCamera(this));
                else
                    world.Remove<components.ActiveCamera>();
                isEnabled = value;
            }
        }

        protected BaseCamera(ITagContainer diContainer)
        {
            world = diContainer.GetTag<DefaultEcs.World>();
            world.SetMaxCapacity<components.ActiveCamera>(1);
            zzContainer = diContainer.GetTag<IZanzarahContainer>();
            camera = diContainer.GetTag<Camera>();

            var game = diContainer.GetTag<Game>();
            playerLocationLazy = new Lazy<Location>(() => game.PlayerEntity.Get<Location>());
        }

        public virtual void Dispose()
        {
            IsEnabled = false;
        }

        public virtual void Update(float state)
        {
        }
    }
}
