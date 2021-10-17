using System;
using System.Collections;
using System.Collections.Generic;

namespace zzre.game.components
{
    public readonly struct Siblings : IReadOnlyList<DefaultEcs.Entity>
    {
        private readonly DefaultEcs.Entity[] _siblings;
        private IReadOnlyList<DefaultEcs.Entity> siblings => _siblings ?? Array.Empty<DefaultEcs.Entity>();

        public Siblings(DefaultEcs.Entity[] siblings) => _siblings = siblings;

        public DefaultEcs.Entity this[int index] => siblings[index];
        public int Count => siblings.Count;
        public IEnumerator<DefaultEcs.Entity> GetEnumerator() => siblings.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
