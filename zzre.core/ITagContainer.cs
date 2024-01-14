﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace zzre;

public interface ITagContainer : IDisposable
{
    bool HasTag<TTag>() where TTag : class;
    TTag GetTag<TTag>() where TTag : class;
    bool TryGetTag<TTag>([NotNullWhen(true)] out TTag tag) where TTag : class;
    IEnumerable<TTag> GetTags<TTag>() where TTag : class;
    ITagContainer AddTag<TTag>(TTag tag) where TTag : class;
    bool RemoveTag<TTag>(bool dispose = true) where TTag : class;
}
