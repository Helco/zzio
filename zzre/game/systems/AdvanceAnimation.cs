using System;
using DefaultEcs.System;

namespace zzre.game.systems
{
    public class AdvanceAnimation : AComponentSystem<float, Skeleton>
    {
        public AdvanceAnimation(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>())
        {
        }

        protected override void Update(float state, ref Skeleton component) => component.AddTime(state);
    }
}
