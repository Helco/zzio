using DefaultEcs.System;

namespace zzre.game.systems
{
    [With(typeof(components.Dead))]
    public partial class Reaper : AEntitySetSystem<float>
    {
        public Reaper(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
        {
        }

        [Update]
        private void Update(in DefaultEcs.Entity entity)
        {
            entity.Dispose();
        }
    }
}
