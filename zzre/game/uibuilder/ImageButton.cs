using DefaultEcs;

namespace zzre.game.uibuilder;

internal sealed record ImageButton : ButtonLike<ImageButton>
{
    public ImageButton(UIBuilder preload, Entity parent) : base(preload, parent)
    {
    }

    public static implicit operator Entity(ImageButton builder) => builder.Build();

    public Entity Build() => BuildBase();
}
