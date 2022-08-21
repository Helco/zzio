using DefaultEcs.System;

namespace zzre.game.systems
{
    public partial class AdvanceAnimation : AEntitySetSystem<float>
    {
        public AdvanceAnimation(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
        {
        }

        [Update]
        private void Update(float elapsedTime, DefaultEcs.Entity entity, ref Skeleton component)
        {
            float factor = entity.Has<components.AnimationSpeed>()
                ? entity.Get<components.AnimationSpeed>().Factor
                : 1f;
            component.AddTime(elapsedTime * factor);
        }
    }
}
