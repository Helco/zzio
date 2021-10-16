namespace zzre.game.components
{
    public readonly struct ActorParts
    {
        public readonly DefaultEcs.Entity Body { get; init; }
        public readonly DefaultEcs.Entity? Wings { get; init; }
    }
}
