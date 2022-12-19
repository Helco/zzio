using System;
using System.Collections.Generic;
using System.Linq;

namespace zzre
{
    internal readonly struct Interval
    {
        public readonly float Min, Max;

        public Interval(float a, float b) => (Min, Max) = (Math.Min(a, b), Math.Max(a, b));
        public Interval(IEnumerable<float> set) => (Min, Max) = (set.Min(), set.Max());

        public Interval Merge(Interval other) => new(Math.Min(Min, other.Min), Math.Max(Max, other.Max));

        public bool Intersects(float point) => point >= Min && point <= Max;
        public bool Intersects(Interval other) => Min <= other.Max && other.Min <= Max;
    }
}
