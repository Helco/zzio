namespace zzre.game.components
{
    public record struct NPCModifier(int Value)
    {
        public static implicit operator NPCModifier(int value) => new() { Value = value };
    }
}
