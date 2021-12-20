using System;
using System.Numerics;
using DefaultEcs.System;

namespace zzre.game.systems.ui
{
    public partial class Cursor : AEntitySetSystem<float>
    {
        private readonly IZanzarahContainer zzContainer;
        private readonly UI ui;

        private Vector2 mousePos;
        private components.ui.HoveredElement? hoveredElement;

        public Cursor(ITagContainer diContainer) : base(diContainer.GetTag<DefaultEcs.World>(), CreateEntityContainer, useBuffer: false)
        {
            zzContainer = diContainer.GetTag<IZanzarahContainer>();
            ui = diContainer.GetTag<UI>();
        }

        protected override void PreUpdate(float state)
        {
            ref var rect = ref ui.CursorEntity.Get<Rect>();
            rect.Center = zzContainer.MousePos / new Vector2(zzContainer.Framebuffer.Width, zzContainer.Framebuffer.Height);
            rect.Center = ui.LogicalScreen.AbsolutePos(rect.Center);
            mousePos = rect.Center;

            if (!ui.CursorEntity.Has<components.Visibility>())
                World.Remove<components.ui.HoveredElement>();
            hoveredElement = null;
        }

        [Update]
        private void Update(
            in DefaultEcs.Entity entity,
            in components.ui.ElementId elementId,
            in Rect elementBounds,
            in components.ui.UIOffset offset)
        {
            var actualBounds = new Rect(
                offset.Calc(elementBounds.Center, ui.LogicalScreen),
                elementBounds.Size);
            if (hoveredElement == null && actualBounds.IsInside(mousePos))
            {
                if (!entity.Has<components.ui.Hovered>())
                {
                    entity.Set<components.ui.Hovered>();
                    // TODO: Play sample g000 when UI element is hovered
                }
                hoveredElement = new(entity, elementId);
            }
            else
                entity.Remove<components.ui.Hovered>();
        }

        protected override void PostUpdate(float state)
        {
            if (hoveredElement == null)
                World.Remove<components.ui.HoveredElement>();
            else
                World.Set(hoveredElement);
        }
    }
}
