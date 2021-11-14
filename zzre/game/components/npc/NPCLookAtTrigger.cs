namespace zzre.game.components
{
    public struct NPCLookAtTrigger
    {
        public readonly int TriggerIdx;
        public float TimeLeft;

        public NPCLookAtTrigger(int triggerIdx, float duration) => (TriggerIdx, TimeLeft) = (triggerIdx, duration);
    }
}
