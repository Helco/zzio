namespace zzre.game.components;

public readonly struct ActorParts(DefaultEcs.Entity body, DefaultEcs.Entity? wings)
{
    public readonly DefaultEcs.Entity Body { get; init; } = body;
    public readonly DefaultEcs.Entity? Wings { get; init; } = wings;
}
