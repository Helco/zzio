using DefaultEcs;
using zzre.game.systems.ui;

namespace zzre.game.uibuilder
{
    internal record ImageButton : ButtonLike<ImageButton>
    {
        public ImageButton(UIPreloader preload, Entity parent) : base(preload, parent)
        {
        }

        public static implicit operator Entity(ImageButton builder) => builder.Build();

        public Entity Build() => BuildBase();
    }
}
