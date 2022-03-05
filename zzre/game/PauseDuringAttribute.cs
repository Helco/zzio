using System;
using System.Collections.Generic;
using System.Linq;

namespace zzre.game
{
    [Flags]
    public enum PauseTrigger
    {
        UIScreen = 1 << 0,
        GameFlow = 1 << 1
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class PauseDuringAttribute : Attribute
    {
        public PauseTrigger Trigger { get; }

        public PauseDuringAttribute(PauseTrigger trigger) => Trigger = trigger;

        public IEnumerable<PauseTrigger> AllTriggers => Enum
            .GetValues<PauseTrigger>()
            .Where(t => Trigger.HasFlag(t));
    }
}
