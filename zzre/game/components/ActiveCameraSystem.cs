using DefaultEcs.System;

namespace zzre.game
{
    public readonly struct ActiveCameraSystem
    {
        public BaseCameraSystem System { get; }

        public ActiveCameraSystem(BaseCameraSystem system) => System = system;
    }
}
