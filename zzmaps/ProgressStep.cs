using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace zzmaps
{
    class ProgressStep
    {
        private long current;

        public string Name { get; }
        public long Current => current;
        public long? Total { get; set; }

        public ProgressStep(string name) => Name = name;

        public void Increment() => Interlocked.Increment(ref current);
    }
}
