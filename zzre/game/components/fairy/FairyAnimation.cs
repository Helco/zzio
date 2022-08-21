namespace zzre.game.components;

public record struct FairyAnimation(
    System.Numerics.Vector3 TargetDirection,
    float WingSpeed,
    zzio.AnimationType Current);
