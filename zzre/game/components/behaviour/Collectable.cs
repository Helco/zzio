using System.Numerics;

namespace zzre.game.components.behaviour;

public struct Collectable
{
    public readonly bool IsDynamic { get; init; } // dynamic item grabs are not saved
    public readonly uint ModelId { get; init; }
    public bool IsDying;
    public float Age;
}

public struct CollectablePhysics
{
    public Vector3 Velocity;
    public float YVelocity; // Velocity is normalized, this one breaks out of that normalization, blame Funatics
}
