using System.Numerics;

namespace zzre.game.uibuilder;

internal abstract record Base<T> where T : Base<T>
{
    protected readonly systems.ui.UIPreloader preload;
    protected DefaultEcs.Entity parent; // not readonly for nested entities like ButtonLabel
    protected Rect rect;
    protected zzio.IColor color = zzio.IColor.White;
    protected int renderOrder = 0;
    protected components.ui.UIOffset offset = components.ui.UIOffset.Center;
    protected zzio.UID? tooltipUID = null;
    protected components.ui.Fade? fade = null;
    protected components.ui.FlashFade? flashFade = null;
    protected components.Visibility visibility = components.Visibility.Visible;

    protected bool HasSize => rect.Size.MaxComponent() > 0.00001f;

    public Base(systems.ui.UIPreloader preload, DefaultEcs.Entity parent)
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

    public T With(components.ui.Fade fade)
    {
        this.fade = fade;
        return (T)this;
    }

    public T With(components.ui.FlashFade flashFade)
    {
        this.flashFade = flashFade;
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
            entity.Set(fade.Value);
        if (flashFade.HasValue)
        {
            entity.Set(flashFade.Value);
            entity.Set(color with { a = (byte)(flashFade.Value.From * 255) });
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
