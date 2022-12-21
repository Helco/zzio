using DefaultEcs;

namespace zzre.game.messages;

public readonly struct NPCMoveSystem
{
    public enum Mode
    {
        FarthestFromPlayer,
        LuckyNearest,
        Random
    }

    public readonly DefaultEcs.Entity Entity;
    public readonly Mode WaypointMode;
    public readonly int WaypointCategory;

    public NPCMoveSystem(Entity entity, Mode waypointMode, int waypointCategory)
    {
        Entity = entity;
        WaypointMode = waypointMode;
        WaypointCategory = waypointCategory;
    }
}
