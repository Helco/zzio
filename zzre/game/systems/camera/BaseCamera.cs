using DefaultEcs.System;
using zzre.rendering;

namespace zzre.game.systems
{
    public class BaseCamera : ISystem<float>
    {
        protected readonly IZanzarahContainer zzContainer;
        protected readonly Camera camera;
        private readonly DefaultEcs.World world;

        private bool isEnabled;
        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (world.Has<components.ActiveCamera>())
                {
                    ref readonly var activeCamera = ref world.Get<components.ActiveCamera>();
                    if (value)
                        activeCamera.System.isEnabled = activeCamera.System == this;
                    else
                        world.Remove<components.ActiveCamera>();
                }
                else
                    world.Set(new components.ActiveCamera(this));
                isEnabled = value;
            }
        }

        protected BaseCamera(ITagContainer diContainer)
        {
            world = diContainer.GetTag<DefaultEcs.World>();
            world.SetMaxCapacity<components.ActiveCamera>(1);
            zzContainer = diContainer.GetTag<IZanzarahContainer>();
            camera = diContainer.GetTag<Camera>();
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
