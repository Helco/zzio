namespace zzre.game.components
{
    public struct NPCLookAtTrigger
    {
        public readonly int TriggerIdx;
        public float TimeLeft;
        public zzio.scn.Trigger? Trigger;

        public NPCLookAtTrigger(int triggerIdx, float duration) => (TriggerIdx, TimeLeft, Trigger) = (triggerIdx, duration, null);
    }
}
