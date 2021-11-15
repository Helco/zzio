using System;
using DefaultEcs.System;

namespace zzre.game.systems
{
    public partial class NPCIdle : AEntitySetSystem<float>
    {
        public NPCIdle(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer2, useBuffer: true)
        {
        }

        private static DefaultEcs.EntitySet CreateEntityContainer2(object sender, DefaultEcs.World world) => world
            .GetEntities()
            .With<components.NPCIdle>()
            .With<components.NPCState>(IsIdleNPCState)
            .AsSet();

        private static bool IsIdleNPCState(in components.NPCState value) => value == components.NPCState.Idle;

        protected override void Update(float state, in DefaultEcs.Entity entity) =>
            Update(state, entity, ref entity.Get<components.NPCIdle>());

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
