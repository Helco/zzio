namespace zzre.game.messages
{
    public readonly struct SceneLoaded
    {
        public readonly int entryId;

        public SceneLoaded(int entryId) => this.entryId = entryId;
    }
}
