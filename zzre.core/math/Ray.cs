using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace zzre
{
    public readonly struct Ray
    {
        public readonly Vector3 Start;
        public readonly Vector3 Direction;

        public Ray(Vector3 start, Vector3 dir) => (Start, Direction) = (start, Vector3.Normalize(dir));

        public bool Intersects(Vector3 point) => MathEx.Cmp(1f, Vector3.Dot(Direction, Vector3.Normalize(point - Start)));
        public float PhaseOf(Vector3 point) => Vector3.Dot(point - Start, Direction);
        public Vector3 ClosestPoint(Vector3 point) => Start + Direction * Math.Max(0f, PhaseOf(point));
    }
}
