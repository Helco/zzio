using System;
using System.Collections.Generic;

namespace zzre
{
    public interface ITagContainer<TBase> where TBase : class
    {
        bool HasTag<TTag>() where TTag : TBase;
        TTag GetTag<TTag>() where TTag : TBase;
        IEnumerable<TTag> GetTags<TTag>() where TTag : TBase;
        ITagContainer<TBase> AddTag<TTag>(TTag tag) where TTag : TBase;
        bool RemoveTag<TTag>() where TTag : TBase;
    }

    public interface ITagContainer : ITagContainer<object> { }
}
