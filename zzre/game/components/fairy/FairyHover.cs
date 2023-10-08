using System.Numerics;

namespace zzre.game.components;

public enum FairyHoverState
{
    Behind = 2,
    KeepLastHover = 10
}

public readonly record struct FairyHoverOffset(Vector3 Value);

public readonly record struct FairyHoverStart(Vector3 Value);

public struct FairyHoverBehind
{
    public enum Mode
    {
        CenterHigh,
        LeftLow,
        LeftHigh,
        RightHigh,
        RightLow,
        CenterLow
    }

    public Mode CurMode;
    public float TimeLeft;

    public float MaxDuration { get; init; }
    public float Distance { get; init; }

    public static readonly FairyHoverBehind Normal = new()
    {
        MaxDuration = 10f,
        Distance = 1f
    };

    public static readonly FairyHoverBehind Erratic = new()
    {
        MaxDuration = 1f,
        Distance = 0.7f
    };
}
