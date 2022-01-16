using System;

namespace zzre.game
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class PauseDuringUIScreenAttribute : Attribute
    {}
}
