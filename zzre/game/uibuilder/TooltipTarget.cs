using DefaultEcs;
using zzre.game.systems.ui;

namespace zzre.game.uibuilder
{
    internal record TooltipTarget : LabelLike<TooltipTarget>
    {
        public TooltipTarget(UIPreloader preload, Entity parent) : base(preload, parent)
        {
            font = preload.Fnt002;
        }

        public static implicit operator Entity(TooltipTarget builder) => builder.Build();

        public Entity Build()
        {
            var prefix = text;
            text = "";
            var entity = BuildBase();
            entity.Set(new components.ui.TooltipTarget(prefix));
            return entity;
        }
    }
}
