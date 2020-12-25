using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace zzre.core.math
{
    public readonly struct Line
    {
        public readonly Vector3 Start;
        public readonly Vector3 End;
        public Vector3 Vector => End - Start;
        public Vector3 Direction => Vector3.Normalize(Vector);
        public float Length => Vector.Length();
        public float LengthSq => Vector.LengthSquared();

        public Line(Vector3 start, Vector3 end) => (Start, End) = (start, end);

        public float PhaseOf(Vector3 point) => Vector3.Dot(point - Start, Vector) / LengthSq;
        public Vector3 ClosestPoint(Vector3 point) => Start + Vector * Math.Clamp(PhaseOf(point), 0f, 1f);
        public bool Intersects(Vector3 point) => MathEx.CmpZero((point - ClosestPoint(point)).LengthSquared());
    }
}
