using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace zzre.core
{
    public class RelativeOrderSolver<T> : IReadOnlyList<T>
    {
        private Func<T, RelativeOrderItem> orderOf;
        private List<T> ordering = new List<T>();

        public RelativeOrderSolver(Func<T, RelativeOrderItem> orderOf)
        {
            this.orderOf = orderOf;
        }

        public bool TrySolveFor(IEnumerable<T> items)
        {
            var itemByOrder = items.ToDictionary(item => orderOf(item), item => item);
            var dependsOn = itemByOrder
                .SelectMany(pair => Enumerable.Concat(
                    pair.Key.Predecessors.Select(pre => (before: itemByOrder[pre], after: pair.Value)),
                    pair.Key.Ancessors.Select(anc => (before: pair.Value, after: itemByOrder[anc]))))
                .Concat(items.Select(item => (before: item, after: item)))
                .GroupBy(pair => pair.after, pair => pair.before)
                .ToDictionary(group => group.Key, group => group.ToList());

            var newOrdering = new List<T>();
            while(dependsOn.Any())
            {
                var nextItem = dependsOn.FirstOrDefault(p => p.Value.Count == 1).Key;
                if (nextItem == null)
                    return false;
                dependsOn.Remove(nextItem);
                newOrdering.Add(nextItem);
                foreach (var deps in dependsOn.Values)
                    deps.Remove(nextItem);
            }

            ordering = newOrdering;
            return true;
        }

        public void SolveFor(IEnumerable<T> items)
        {
            if (!TrySolveFor(items))
                throw new ArgumentException("Could not find valid ordering");
        }

        public T this[int index] => ordering[index];
        public int Count => ordering.Count;
        public IEnumerator<T> GetEnumerator() => ordering.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
