using System;
using System.Threading;

namespace zzre
{
    public static class GlobalRandom
    {
        private static readonly ThreadLocal<Random> lazyRandom = new ThreadLocal<Random>(CreateNewRandom);

        private static Random CreateNewRandom() => new Random(unchecked(HashCode.Combine(
            Thread.CurrentThread.ManagedThreadId,
            Environment.TickCount)));

        public static Random Get => lazyRandom.Value!;
    }
}
