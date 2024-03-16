namespace zzre.game.messages;

public readonly record struct StartBattle(
    zzio.Savegame Savegame,
    DefaultEcs.Entity OverworldPlayer,
    DefaultEcs.Entity[] OverworldEntities,
    int SceneId,
    bool CanFlee)
{
}
