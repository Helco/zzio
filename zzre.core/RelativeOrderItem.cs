using System;
using System.Collections.Generic;
using System.Linq;

namespace zzre.core
{
    public class RelativeOrderItem
    {
        private List<RelativeOrderItem> predecessors = new List<RelativeOrderItem>();
        private List<RelativeOrderItem> ancessors = new List<RelativeOrderItem>();

        public IReadOnlyCollection<RelativeOrderItem> Predecessors => predecessors;
        public IReadOnlyCollection<RelativeOrderItem> Ancessors => ancessors;

        public RelativeOrderItem Before(RelativeOrderItem other)
        {
            ancessors.Add(other);
            return this;
        }

        public RelativeOrderItem After(RelativeOrderItem other)
        {
            predecessors.Add(other);
            return this;
        }
    }
}
