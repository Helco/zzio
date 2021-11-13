namespace zzre.game.components
{
    public readonly struct NPCModifier
    {
        public readonly int Value { get; init; }

        public static implicit operator NPCModifier(int value) => new NPCModifier { Value = value };
    }
}
