using System.Numerics;

namespace zzre.game.components;

public struct PlantWiggle
{
    public Quaternion StartRotation { get; init; }
    public Vector2 Amplitude { get; init; }
    public float Delay { get; init; }
    public float RemainingTimer;
    public Vector2 Angles; // to prevent accumulation of roll
}
