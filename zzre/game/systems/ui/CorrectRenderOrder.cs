using System;
using DefaultEcs.System;

namespace zzre.game.systems.ui
{
    public partial class CorrectRenderOrder : AEntitySetSystem<float>
    {
        public CorrectRenderOrder(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: true)
        {
        }

        [Update]
        private void Update(
            in DefaultEcs.Entity entity,
            in components.ui.RenderOrder renderOrder,
            materials.UIMaterial? material)
        {
            // use entity.Set to update sorted entity set used by batcher

            int materialHash = material?.GetHashCode() ?? int.MinValue;
            if (renderOrder.MaterialHash != materialHash)
                entity.Set(renderOrder with { MaterialHash = materialHash });
        }
    }
}
