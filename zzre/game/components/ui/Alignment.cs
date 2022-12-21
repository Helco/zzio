using System.Numerics;

namespace zzre.game.components.ui;

public enum Alignment
{
    Min,
    Max,
    Center
}

public record struct FullAlignment(Alignment Horizontal, Alignment Vertical)
{
    private static float GetAsFactor(Alignment a) => a switch
    {
        Alignment.Min => 0f,
        Alignment.Max => 1f,
        Alignment.Center => 0.5f,
        _ => throw new System.NotSupportedException($"Unsupported alignment: {a}")
    };

    public Vector2 AsFactor => new(GetAsFactor(Horizontal), GetAsFactor(Vertical));

    public static readonly FullAlignment Center = new(Alignment.Center, Alignment.Center);
    public static readonly FullAlignment TopLeft = default;
    public static readonly FullAlignment TopCenter = new(Alignment.Center, Alignment.Min);
    public static readonly FullAlignment TopRight = new(Alignment.Max, Alignment.Min);

    public static readonly FullAlignment CenterLeft = new(Alignment.Min, Alignment.Center);
    public static readonly FullAlignment CenterRight = new(Alignment.Max, Alignment.Center);
}
