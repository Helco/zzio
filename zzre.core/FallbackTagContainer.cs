﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace zzre.core
{
    public class FallbackTagContainer : BaseDisposable, ITagContainer
    {
        private readonly ITagContainer fallback;
        private readonly ITagContainer main;

        public FallbackTagContainer(ITagContainer main, ITagContainer fallback)
        {
            this.main = main;
            this.fallback = fallback;
        }

        public TTag GetTag<TTag>() where TTag : class
        {
            if (main.TryGetTag<TTag>(out var tag))
                return tag;
            return fallback.GetTag<TTag>();
        }

        public bool TryGetTag<TTag>([NotNullWhen(true)] out TTag tag) where TTag : class =>
            main.TryGetTag(out tag) || fallback.TryGetTag(out tag);

        public IEnumerable<TTag> GetTags<TTag>() where TTag : class =>
            main.GetTags<TTag>().Union(fallback.GetTags<TTag>());

        public bool HasTag<TTag>() where TTag : class =>
            main.HasTag<TTag>() || fallback.HasTag<TTag>();

        public bool RemoveTag<TTag>() where TTag : class =>
            throw new NotSupportedException("FallbackTagContainer should not be modified");

        public ITagContainer AddTag<TTag>(TTag tag) where TTag : class =>
            throw new NotSupportedException("FallbackTagContainer should not be modified");
    }

    public static class FallbackTagContainerExtensions
    {
        public static ITagContainer FallbackTo(this ITagContainer main, ITagContainer fallback) =>
            new FallbackTagContainer(main, fallback);
    }
}
