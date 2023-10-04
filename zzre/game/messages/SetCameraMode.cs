using System;

namespace zzre.game.messages;

public record struct SetCameraMode(int Mode, DefaultEcs.Entity TargetEntity)
{
    public static readonly SetCameraMode Overworld = new(-1, default);

    public enum SubMode
    {
        LeftTop = 0,
        LeftBottom,
        LeftCenter,
        RightTop,
        RightBottom,
        RightCenter,
        Overworld, // ignored by CreatureCamera, Funatics and its special cases -_-
        Behind,
        Front
    }

    public static SetCameraMode PlayerToTarget(SubMode subMode, DefaultEcs.Entity target) =>
        new(1000 + (int)subMode, target);

    public static SetCameraMode TargetToPlayer(SubMode subMode, DefaultEcs.Entity target) =>
        new(2000 + (int)subMode, target);

    public static SetCameraMode PlayerToBehind(DefaultEcs.Entity target) =>
        PlayerToTarget(SubMode.Behind, target);
};
