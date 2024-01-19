using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using zzio;

namespace zzre;

public class ExtendedTagContainer : BaseDisposable, ITagContainer
{
    private readonly ITagContainer parent;
    private readonly TagContainer extension = new();

    public ExtendedTagContainer(ITagContainer parent)
    {
        this.parent = parent;
    }

    protected override void DisposeManaged() => extension.Dispose();

    public ITagContainer AddTag<TTag>(TTag tag) where TTag : class
    {
        extension.AddTag(tag);
        return this;
    }

    public bool TryGetTag<TTag>([NotNullWhen(true)] out TTag tag) where TTag : class =>
        extension.TryGetTag(out tag) || parent.TryGetTag(out tag);

    public TTag GetTag<TTag>() where TTag : class
    {
        if (extension.TryGetTag<TTag>(out var tag))
            return tag;
        return parent.GetTag<TTag>();
    }

    public IEnumerable<TTag> GetTags<TTag>() where TTag : class =>
        extension.GetTags<TTag>().Union(parent.GetTags<TTag>());

    public bool HasTag<TTag>() where TTag : class =>
        extension.HasTag<TTag>() || parent.HasTag<TTag>();

    public bool RemoveTag<TTag>(bool dispose = true) where TTag : class =>
        extension.RemoveTag<TTag>(dispose);
}

public static class TagContainerExtensions
{
    public static ITagContainer ExtendedWith<T1>(this ITagContainer parent, T1 t1)
        where T1 : class =>
        new ExtendedTagContainer(parent).AddTag(t1);

    public static ITagContainer ExtendedWith<T1, T2>(this ITagContainer parent, T1 t1, T2 t2)
        where T1 : class
        where T2 : class =>
        new ExtendedTagContainer(parent).AddTag(t1).AddTag(t2);

    public static ITagContainer ExtendedWith<T1, T2, T3>(this ITagContainer parent, T1 t1, T2 t2, T3 t3)
        where T1 : class
        where T2 : class
        where T3 : class =>
        new ExtendedTagContainer(parent).AddTag(t1).AddTag(t2).AddTag(t3);

    public static ITagContainer ExtendedWith<T1, T2, T3, T4>(this ITagContainer parent, T1 t1, T2 t2, T3 t3, T4 t4)
        where T1 : class
        where T2 : class
        where T3 : class
        where T4 : class =>
        new ExtendedTagContainer(parent).AddTag(t1).AddTag(t2).AddTag(t3).AddTag(t4);
}
