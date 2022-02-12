namespace zzre.game.components
{
    public enum DialogState
    {
        NextScriptOp,
        FadeOut,
        Say, // might be split into ~5 other states
        PreFightWild,
        PreFightNpc,
        Npcwalking,
        NpcEscapes,
        CaughtFairy,
        Delay
    }
}
