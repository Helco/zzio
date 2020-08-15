using System;
using System.Numerics;

namespace zzre
{
    public static class NumericsExtensions
    {
        private const float EPS = 0.001f;

        public static Vector3 SomeOrthogonal(this Vector3 v) => Math.Abs(v.Y) < EPS && Math.Abs(v.Z) < EPS
            ? Vector3.Cross(v, Vector3.UnitY)
            : Vector3.Cross(v, Vector3.UnitX);
    }
}
