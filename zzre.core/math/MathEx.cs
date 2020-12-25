using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace zzre
{
    public static class MathEx
    {
        public static bool Cmp(float a, float b) =>
            Math.Abs(a - b) <= float.Epsilon * Math.Max(1f, Math.Max(Math.Abs(a), Math.Abs(b)));

        public static bool CmpZero(float a) => Cmp(a, 0.0f);

        public static bool SATIntersects(IEnumerable<Vector3> pointsA, IEnumerable<Vector3> pointsB, IEnumerable<Vector3> axesA, IEnumerable<Vector3> axesB) =>
            SATIntersects(pointsA, pointsB,
                axesA.Concat(axesB).Concat(axesA.SelectMany(a => axesB.Select(b => Vector3.Cross(a, b)))));

        public static bool SATIntersects(IEnumerable<Vector3> pointsA, IEnumerable<Vector3> pointsB, IEnumerable<Vector3> axes) =>
            axes.All(axis => !
                new Interval(pointsA.Select(p => Vector3.Dot(p, axis))).Intersects(
                new Interval(pointsB.Select(p => Vector3.Dot(p, axis)))));
    }
}
