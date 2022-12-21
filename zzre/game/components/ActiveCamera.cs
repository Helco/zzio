namespace zzre.game.components;

public readonly struct ActiveCamera
{
    public readonly systems.BaseCamera System;

    public ActiveCamera(systems.BaseCamera system) => System = system;
}
