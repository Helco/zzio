using System.Collections.Generic;

namespace zzio;

public enum AnimationType
{
    Idle0 = 0,
    Jump,
    Run,
    RunForwardLeft,
    RunForwardRight,
    Back,
    Dance,
    Fall,
    Rotate,
    Right,
    Left,
    Idle1,
    Idle2,
    Talk0,
    Talk1,
    Talk2,
    Talk3,
    Walk0,
    Walk1,
    Walk2,
    SpecialIdle0,
    SpecialIdle1,
    SpecialIdle2,
    FlyForward,
    FlyBack,
    FlyLeft,
    FlyRight,
    Loadup,
    Hit,
    Joy,
    ThudGround,
    UseFairyPipe,
    UseSeaShell,
    Smith,
    Astonished,
    Surprise0,
    Surprise1,
    Stop,
    ThudGround2,
    PixieFlounder,
    JumpHigh
}

public static class AnimationTypeExtension
{
    private static readonly IReadOnlyDictionary<AnimationType, bool> isLooping =
        new Dictionary<AnimationType, bool>()
        {
            { AnimationType.Idle0,              true },
            { AnimationType.Jump,               false },
            { AnimationType.Run,                true },
            { AnimationType.RunForwardLeft,     true },
            { AnimationType.RunForwardRight,    true },
            { AnimationType.Back,               true },
            { AnimationType.Dance,              true },
            { AnimationType.Fall,               true },
            { AnimationType.Rotate,             true },
            { AnimationType.Right,              true },
            { AnimationType.Left,               true },
            { AnimationType.Idle1,              true },
            { AnimationType.Idle2,              true },
            { AnimationType.Talk0,              false },
            { AnimationType.Talk1,              false },
            { AnimationType.Talk2,              false },
            { AnimationType.Talk3,              false },
            { AnimationType.Walk0,              true },
            { AnimationType.Walk1,              true },
            { AnimationType.Walk2,              true },
            { AnimationType.SpecialIdle0,       true },
            { AnimationType.SpecialIdle1,       true },
            { AnimationType.SpecialIdle2,       true },
            { AnimationType.FlyForward,         true },
            { AnimationType.FlyBack,            true },
            { AnimationType.FlyLeft,            true },
            { AnimationType.FlyRight,           true },
            { AnimationType.Loadup,             true },
            { AnimationType.Hit,                false },
            { AnimationType.Joy,                true },
            { AnimationType.ThudGround,         false },
            { AnimationType.UseFairyPipe,       false },
            { AnimationType.UseSeaShell,         false },
            { AnimationType.Smith,              true },
            { AnimationType.Astonished,         false },
            { AnimationType.Surprise0,          true },
            { AnimationType.Surprise1,          true },
            { AnimationType.Stop,               false },
            { AnimationType.ThudGround2,        false },
            { AnimationType.PixieFlounder,      true },
            { AnimationType.JumpHigh,           false }
        };
    public static bool IsLooping(this AnimationType type)
    {
        return isLooping[type];
    }
}
