using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using zzre.core;

namespace zzre
{
    public class TagContainer<TBase> : BaseDisposable, ITagContainer<TBase> where TBase : class
    {
        private Dictionary<Type, TBase> tags = new Dictionary<Type, TBase>();

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            foreach (var disposableTag in GetRawTags<IDisposable>())
                (disposableTag as IDisposable)?.Dispose();
        }

        public bool HasTag<TTag>() where TTag : TBase => TryGetTag<TTag>(out var _);
        
        public bool TryGetTag<TTag>([MaybeNullWhen(false)] out TTag tag) where TTag : TBase
        {
            tag = default;
            TBase? tagBase = default;
            if (!tags.TryGetValue(typeof(TTag), out tagBase))
                tagBase = tags.FirstOrDefault(p => typeof(TTag).IsAssignableFrom(p.Key)).Value;
            if (tagBase == null)
                return false;

            tag = (TTag)tagBase;
            return true;
        }

        public TTag GetTag<TTag>() where TTag : TBase
        {
            if (TryGetTag<TTag>(out var tag))
                return tag;
            throw new ArgumentOutOfRangeException(nameof(TTag), $"No tag of type {typeof(TTag).Name} is attached");
        }

        public IEnumerable<TTag> GetTags<TTag>() where TTag : TBase => tags
            .Where(pair => typeof(TTag).IsAssignableFrom(pair.Key))
            .Select(pair => (TTag)pair.Value);

        public IEnumerable<TBase> GetRawTags<TTag>() => tags
            .Where(pair => typeof(TTag).IsAssignableFrom(pair.Key))
            .Select(pair => pair.Value);

        public ITagContainer<TBase> AddTag<TTag>(TTag tag) where TTag : TBase
        {
            if (!tags.TryAdd(typeof(TTag), tag))
                throw new ArgumentException(nameof(TTag), $"A tag of type {typeof(TTag).Name} is already attached");
            return this;
        }

        public bool RemoveTag<TTag>() where TTag : TBase
        {
            if (tags.Remove(typeof(TTag)))
                return true;
            var pair = tags.FirstOrDefault(p => typeof(TTag).IsAssignableFrom(p.Key));
            return pair.Key != null && tags.Remove(pair.Key);
        }
    }

    public class TagContainer : TagContainer<object>, ITagContainer { }
}
