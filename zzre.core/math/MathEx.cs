using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace zzre
{
    public static class MathEx
    {
        public const float DegToRad = MathF.PI / 180f;
        public const float RadToDeg = 180f / MathF.PI;

        public static bool Cmp(float a, float b) =>
            Math.Abs(a - b) <= float.Epsilon * Math.Max(1f, Math.Max(Math.Abs(a), Math.Abs(b)));

        public static bool CmpZero(float a) => Math.Abs(a) < 0.1E-10;

        public static Vector3 Project(Vector3 length, Vector3 dir)
        {
            var dot = Vector3.Dot(length, dir);
            return dir * (dot / dir.LengthSquared());
        }

        public static Vector3 SafeNormalize(Vector3 v)
        {
            var lengthSqr = v.LengthSquared();
            return CmpZero(lengthSqr)
                ? Vector3.Zero
                : v * (1f / lengthSqr);
        }

        public static Vector3 Perpendicular(Vector3 length, Vector3 dir) =>
            length - Project(length, dir);

        public static bool SATIntersects(IEnumerable<Vector3> pointsA, IEnumerable<Vector3> pointsB, IEnumerable<Vector3> axesA, IEnumerable<Vector3> axesB) =>
            SATIntersects(pointsA, pointsB,
                axesA.Concat(axesB).Concat(axesA.SelectMany(a => axesB.Select(b => Vector3.Cross(a, b)))));

        public static bool SATIntersects(IEnumerable<Vector3> pointsA, IEnumerable<Vector3> pointsB, IEnumerable<Vector3> axes)
        {
            foreach (var axis in axes)
            {
                var i1 = new Interval(pointsA.Select(p => Vector3.Dot(p, axis)));
                var i2 = new Interval(pointsB.Select(p => Vector3.Dot(p, axis)));
                if (!CmpZero(axis.LengthSquared()) && !i1.Intersects(i2))
                    return false;
            }
            return true;
        }

        public static Vector3 SATCrossEdge(Line e1, Line e2)
        {
            var cross = Vector3.Cross(e1.Vector, e2.Vector);
            return CmpZero(cross.LengthSquared())
                ? Vector3.Cross(e1.Vector, Vector3.Cross(e1.Vector, e2.Start - e1.Start))
                : cross;
        }

        public static Vector3 HorizontalSlerp(Vector3 from, Vector3 to, float curvature, float time)
        {
            var fromAngle = MathF.Atan2(from.X, from.Z);
            var angleDelta = MathF.Atan2(to.X, to.Z) - fromAngle;
            if (angleDelta < -MathF.PI)
                angleDelta += 2 * MathF.PI;
            if (angleDelta > MathF.PI)
                angleDelta -= 2 * MathF.PI;
            var newAngle = (1f - 1f / MathF.Pow(curvature, time)) * angleDelta + fromAngle;

            return new Vector3(
                MathF.Sin(newAngle),
                to.Y,
                MathF.Cos(newAngle));
        }
    }
}
