using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using zzio;

namespace zzre
{
    public class TagContainer : BaseDisposable, ITagContainer
    {
        private readonly Dictionary<Type, object> tags = new Dictionary<Type, object>();

        protected override void DisposeManaged()
        {
            base.DisposeManaged();
            foreach (var disposableTag in GetRawTags<IDisposable>())
                (disposableTag as IDisposable)?.Dispose();
        }

        public bool HasTag<TTag>() where TTag : class => TryGetTag<TTag>(out var _);

        public bool TryGetTag<TTag>([NotNullWhen(true)] out TTag tag) where TTag : class
        {
            tag = default!;
            object? tagBase = default;
            if (!tags.TryGetValue(typeof(TTag), out tagBase))
                tagBase = tags.FirstOrDefault(p => typeof(TTag).IsAssignableFrom(p.Value.GetType())).Value;
            if (tagBase == null)
                return false;

            tag = (TTag)tagBase;
            return true;
        }

        public TTag GetTag<TTag>() where TTag : class
        {
            if (TryGetTag<TTag>(out var tag))
                return tag;
            throw new ArgumentOutOfRangeException(nameof(TTag), $"No tag of type {typeof(TTag).Name} is attached");
        }

        public IEnumerable<TTag> GetTags<TTag>() where TTag : class => tags
            .Where(pair => typeof(TTag).IsAssignableFrom(pair.Key))
            .Select(pair => (TTag)pair.Value);

        public IEnumerable<object> GetRawTags<TTag>() => tags
            .Where(pair => typeof(TTag).IsAssignableFrom(pair.Key))
            .Select(pair => pair.Value);

        public ITagContainer AddTag<TTag>(TTag tag) where TTag : class
        {
            if (tag == null)
                throw new NullReferenceException();
            if (!tags.TryAdd(typeof(TTag), tag))
                throw new ArgumentException($"A tag of type {typeof(TTag).Name} is already attached", nameof(TTag));
            return this;
        }

        public bool RemoveTag<TTag>() where TTag : class
        {
            if (tags.Remove(typeof(TTag)))
                return true;
            var pair = tags.FirstOrDefault(p => typeof(TTag).IsAssignableFrom(p.Key));
            return pair.Key != null && tags.Remove(pair.Key);
        }
    }
}
