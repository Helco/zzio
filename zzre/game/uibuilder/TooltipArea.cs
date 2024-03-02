using DefaultEcs;

namespace zzre.game.uibuilder;

internal sealed record TooltipArea : Identified<TooltipArea>
{
    public TooltipArea(UIBuilder preload, Entity parent) : base(preload, parent)
    {
    }

    public static implicit operator Entity(TooltipArea builder) => builder.Build();

    public Entity Build() => BuildBase();
}
