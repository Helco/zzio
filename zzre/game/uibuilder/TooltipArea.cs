using DefaultEcs;
using zzre.game.systems.ui;

namespace zzre.game.uibuilder
{
    internal record TooltipArea : Identified<TooltipArea>
    {
        public TooltipArea(UIPreloader preload, Entity parent) : base(preload, parent)
        {
        }

        public static implicit operator Entity(TooltipArea builder) => builder.Build();

        public Entity Build() => BuildBase();
    }
}
