using System.Numerics;

namespace zzre.game.uibuilder;

internal abstract record Base<T> where T : Base<T>
{
    protected readonly UIBuilder preload;
    protected DefaultEcs.Entity parent; // not readonly for nested entities like ButtonLabel
    protected Rect rect;
    protected zzio.IColor color = zzio.IColor.White;
    protected int renderOrder;
    protected components.ui.UIOffset offset = components.ui.UIOffset.Center;
    protected zzio.UID? tooltipUID;
    protected components.ui.Fade? fade;
    protected components.Visibility visibility = components.Visibility.Visible;

    protected bool HasSize => rect.Size.MaxComponent() > 0.00001f;

    public Base(UIBuilder preload, DefaultEcs.Entity parent)
    {
        this.parent = parent;
        this.preload = preload;
    }

    public T With(Vector2 pos)
    {
        rect = Rect.FromMinMax(pos, pos);
        return (T)this;
    }

    public T With(Rect rect)
    {
        this.rect = rect;
        return (T)this;
    }

    public T With(zzio.IColor color)
    {
        this.color = color;
        return (T)this;
    }

    public T WithRenderOrder(int renderOrder)
    {
        this.renderOrder = renderOrder;
        return (T)this;
    }

    public T With(components.ui.UIOffset offset)
    {
        this.offset = offset;
        return (T)this;
    }

    public T WithTooltip(zzio.UID uid)
    {
        tooltipUID = uid;
        return (T)this;
    }

    public T WithTooltip(uint rawUID)
    {
        tooltipUID = new zzio.UID(rawUID);
        return (T)this;
    }

    public T With(components.ui.Fade flashFade)
    {
        this.fade = flashFade;
        return (T)this;
    }

    public T Invisible()
    {
        visibility = components.Visibility.Invisible;
        return (T)this;
    }

    protected virtual DefaultEcs.Entity BuildBase()
    {
        var entity = preload.UIWorld.CreateEntity();
        if (parent != default)
            entity.Set(new components.Parent(parent));
        entity.Set(new components.ui.RenderOrder(renderOrder));
        entity.Set(visibility);
        entity.Set(color);
        entity.Set(rect);
        entity.Set(offset);
        if (tooltipUID.HasValue)
            entity.Set(new components.ui.TooltipUID(tooltipUID.Value));
        if (fade.HasValue)
        {
            entity.Set(fade.Value);
            entity.Set(color with { a = (byte)(fade.Value.From * 255) });
        }
        return entity;
    }

    protected void AlignToSize(DefaultEcs.Entity entity, Vector2 newSize, components.ui.FullAlignment? align = null)
    {
        align ??= components.ui.FullAlignment.TopLeft;
        rect = Rect.FromTopLeftSize(rect.Min - MathEx.Floor(align.Value.AsFactor * newSize), newSize);
        entity.Set(rect);
    }
}
