using DefaultEcs;
using zzre.game.systems.ui;

namespace zzre.game.uibuilder;

internal abstract record Identified<T> : Base<T> where T : Identified<T>
{
    protected components.ui.ElementId? elementId;

    protected Identified(UIPreloader preload, Entity parent) : base(preload, parent)
    {
    }

    public T With(components.ui.ElementId elementId)
    {
        this.elementId = elementId;
        return (T)this;
    }

    protected override Entity BuildBase()
    {
        if (!elementId.HasValue)
            throw new System.InvalidOperationException("UI element has no identifier");
        var entity = base.BuildBase();
        entity.Set(elementId.Value);
        return entity;
    }
}
