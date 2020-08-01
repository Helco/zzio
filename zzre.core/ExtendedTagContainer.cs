using System;
using System.Collections.Generic;
using System.Linq;

namespace zzre.core
{
    public class ExtendedTagContainer<TBase> : BaseDisposable, ITagContainer<TBase> where TBase : class
    {
        private readonly ITagContainer<TBase> parent;
        private readonly TagContainer<TBase> extension = new TagContainer<TBase>();

        public ExtendedTagContainer(ITagContainer<TBase> parent)
        {
            this.parent = parent;
        }

        protected override void DisposeManaged() => extension.Dispose();

        public ITagContainer<TBase> AddTag<TTag>(TTag tag) where TTag : TBase
        {
            extension.AddTag(tag);
            return this;
        }

        public TTag GetTag<TTag>() where TTag : TBase
        {
            if (extension.TryGetTag<TTag>(out var tag))
                return tag;
            return parent.GetTag<TTag>();
        }

        public IEnumerable<TTag> GetTags<TTag>() where TTag : TBase =>
            // TODO: Add test about override behaviour
            extension.GetTags<TTag>().Union(parent.GetTags<TTag>());

        public bool HasTag<TTag>() where TTag : TBase =>
            extension.HasTag<TTag>() || parent.HasTag<TTag>();

        public bool RemoveTag<TTag>() where TTag : TBase =>
            extension.RemoveTag<TTag>();
    }

    public static class TagContainerExtensions
    {
        public static ITagContainer<TBase> ExtendedWith<TBase, T1>(this ITagContainer<TBase> parent, T1 t1)
            where TBase : class
            where T1 : TBase =>
            new ExtendedTagContainer<TBase>(parent).AddTag(t1);

        public static ITagContainer<TBase> ExtendedWith<TBase, T1, T2>(this ITagContainer<TBase> parent, T1 t1, T2 t2)
            where TBase : class
            where T1 : TBase
            where T2 : TBase =>
            new ExtendedTagContainer<TBase>(parent).AddTag(t1).AddTag(t2);

        public static ITagContainer<TBase> ExtendedWith<TBase, T1, T2, T3>(this ITagContainer<TBase> parent, T1 t1, T2 t2, T3 t3)
            where TBase : class
            where T1 : TBase
            where T2 : TBase
            where T3 : TBase =>
            new ExtendedTagContainer<TBase>(parent).AddTag(t1).AddTag(t2).AddTag(t3);

        public static ITagContainer<TBase> ExtendedWith<TBase, T1, T2, T3, T4>(this ITagContainer<TBase> parent, T1 t1, T2 t2, T3 t3, T4 t4)
            where TBase : class
            where T1 : TBase
            where T2 : TBase
            where T3 : TBase
            where T4 : TBase =>
            new ExtendedTagContainer<TBase>(parent).AddTag(t1).AddTag(t2).AddTag(t3).AddTag(t4);
    }
}
