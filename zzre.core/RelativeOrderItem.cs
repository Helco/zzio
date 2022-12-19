﻿using System.Collections.Generic;

namespace zzre.core
{
    public class RelativeOrderItem
    {
        private readonly List<RelativeOrderItem> predecessors = new();
        private readonly List<RelativeOrderItem> ancessors = new();

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
