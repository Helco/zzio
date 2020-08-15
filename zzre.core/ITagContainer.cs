using System;
using System.Collections.Generic;

namespace zzre
{
    public interface ITagContainer
    {
        bool HasTag<TTag>() where TTag : class;
        TTag GetTag<TTag>() where TTag : class;
        IEnumerable<TTag> GetTags<TTag>() where TTag : class;
        ITagContainer AddTag<TTag>(TTag tag) where TTag : class;
        bool RemoveTag<TTag>() where TTag : class;
    }
}
