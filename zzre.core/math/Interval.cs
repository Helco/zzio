using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace zzre;

internal readonly struct Interval
{
    public readonly float Min, Max;

    [MethodImpl(MathEx.MIOptions)]
    public Interval(float a, float b) => (Min, Max) = (Math.Min(a, b), Math.Max(a, b));
    [MethodImpl(MathEx.MIOptions)]
    public Interval(IEnumerable<float> set) => (Min, Max) = (set.Min(), set.Max());

    [MethodImpl(MathEx.MIOptions)]
    public Interval Merge(Interval other) => new(Math.Min(Min, other.Min), Math.Max(Max, other.Max));

    [MethodImpl(MathEx.MIOptions)]
    public bool Intersects(float point) => point >= Min && point <= Max;
    [MethodImpl(MathEx.MIOptions)]
    public bool Intersects(Interval other) => Min <= other.Max && other.Min <= Max;
}
