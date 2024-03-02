using DefaultEcs;

namespace zzre.game.uibuilder;

internal sealed record Label : LabelLike<Label>
{
    public Label(UIBuilder preload, Entity parent) : base(preload, parent)
    {
    }

    public static implicit operator Entity(Label builder) => builder.Build();

    public Entity Build() => BuildBase();
}
