using System;
using System.Numerics;
using DefaultEcs.System;
using zzre.rendering;

namespace zzre.game
{
    public class BaseCameraSystem : ISystem<float>
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
                if (world.Has<ActiveCameraSystem>())
                {
                    ref readonly var activeCamera = ref world.Get<ActiveCameraSystem>();
                    if (value)
                        activeCamera.System.isEnabled = activeCamera.System == this;
                    else
                        world.Remove<ActiveCameraSystem>();
                }
                else
                    world.Set(new ActiveCameraSystem(this));
                isEnabled = value;
            }
        }

        protected BaseCameraSystem(ITagContainer diContainer)
        {
            world = diContainer.GetTag<DefaultEcs.World>();
            world.SetMaxCapacity<ActiveCameraSystem>(1);
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
