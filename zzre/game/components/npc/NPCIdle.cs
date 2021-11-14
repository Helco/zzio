namespace zzre.game.components
{
    public struct NPCIdle
    {
        public float TimeLeft;

        public static readonly NPCIdle Default = new NPCIdle() { TimeLeft = 0.1f };
    }
}
