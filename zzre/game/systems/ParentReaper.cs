using DefaultEcs.System;

namespace zzre.game.systems
{
    public partial class ParentReaper : AEntitySetSystem<float>
    {
        public ParentReaper(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
        {
        }

        [Update]
        private void Update(in DefaultEcs.Entity entity, in components.Parent parent)
        {
            if (!parent.Entity.IsAlive)
                entity.Dispose();
        }
    }
}
