using DefaultEcs;
using zzre.game.systems.ui;

namespace zzre.game.uibuilder;

internal record Label : LabelLike<Label>
{
    public Label(UIPreloader preload, Entity parent) : base(preload, parent)
    {
    }

    public static implicit operator Entity(Label builder) => builder.Build();

    public Entity Build() => BuildBase();
}
