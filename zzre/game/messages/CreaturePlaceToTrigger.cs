using DefaultEcs;

namespace zzre.game.messages
{
    public readonly struct CreaturePlaceToTrigger
    {
        public readonly DefaultEcs.Entity Entity;
        public readonly int TriggerIdx;
        public readonly bool OrientByTrigger;
        public readonly bool MoveToGround;

        public CreaturePlaceToTrigger(Entity entity, int triggerIdx, bool orientByTrigger = true, bool moveToGround = true)
        {
            Entity = entity;
            TriggerIdx = triggerIdx;
            OrientByTrigger = orientByTrigger;
            MoveToGround = moveToGround;
        }
    }
}
