namespace zzre.game.components
{
    public readonly struct ActiveCamera
    {
        public readonly systems.BaseCamera system;

        public ActiveCamera(systems.BaseCamera system) => this.system = system;
    }
}
