namespace zzre.game.messages
{
    public readonly struct NPCChangeWaypoint
    {
        public readonly DefaultEcs.Entity Entity;
        public readonly int FromWaypoint;
        public readonly int ToWaypoint;

        public NPCChangeWaypoint(DefaultEcs.Entity entity, int from, int to)
        {
            Entity = entity;
            FromWaypoint = from;
            ToWaypoint = to;
        }
    }
}
