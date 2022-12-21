using System.Numerics;
using DefaultEcs;
using zzre.game.systems.ui;

namespace zzre.game.uibuilder;

internal record ButtonLabel : LabelLike<ButtonLabel>
{
    private readonly Button parentButton;

    public ButtonLabel(UIPreloader preload, Entity parent, Button parentButton) : base(preload, parent)
    {
        this.parentButton = parentButton;
        textAlign = components.ui.FullAlignment.Center;
        useTotalFontHeight = true;
    }

    public Button Return() => parentButton;

    internal Entity BuildForParent(Entity parent)
    {
        this.parent = parent;
        return BuildBase();
    }

    public static implicit operator Entity(ButtonLabel builder) => builder.Build();
    public Entity Build() => parentButton.Build();

    internal components.ui.FullAlignment Alignment => textAlign!.Value;
}

internal record Button : ButtonLike<Button>
{
    private const float ButtonTextSpacing = 10f;

    private ButtonLabel? label;

    public Button(UIPreloader preload, Entity parent) : base(preload, parent)
    {
    }

    public ButtonLabel WithLabel() => label ??= new ButtonLabel(preload, parent, this);

    public static implicit operator Entity(Button builder) => builder.Build();

    public Entity Build()
    {
        var btnEntity = BuildBase();
        if (label == null)
            return btnEntity;

        var buttonRect = btnEntity.Get<Rect>();
        label
            .With(buttonRect
                .GrownBy(-2 * ButtonTextSpacing, 0f)
                .AbsolutePos(label.Alignment.AsFactor))
            .WithRenderOrder(renderOrder - 1)
            .BuildForParent(btnEntity);

        return btnEntity;
    }
}
