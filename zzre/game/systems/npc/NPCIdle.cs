using DefaultEcs.System;

namespace zzre.game.systems
{
    [PauseDuringUIScreen]
    public partial class NPCIdle : AEntitySetSystem<float>
    {
        public NPCIdle(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
        {
        }

        [WithPredicate]
        private static bool IsIdleNPCState(in components.NPCState value) => value == components.NPCState.Idle;

        [Update]
        private void Update(
            float elapsedTime,
            in DefaultEcs.Entity entity,
            ref components.NPCIdle idle)
        {
            idle.TimeLeft -= elapsedTime;
            if (idle.TimeLeft < 0f)
                entity.Set(components.NPCState.Script);
        }
    }
}
