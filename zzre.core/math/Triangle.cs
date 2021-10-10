using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using static zzre.MathEx;

namespace zzre
{
    public readonly struct Triangle : IRaycastable, IIntersectable
    {
        public readonly Vector3 A, B, C;
        public Line AB => new Line(A, B);
        public Line AC => new Line(A, C);
        public Line BA => new Line(B, A);
        public Line BC => new Line(B, C);
        public Line CA => new Line(C, A);
        public Line CB => new Line(C, B);
        public Vector3 NormalUn => Vector3.Cross(AB.Vector, AC.Vector);
        public Vector3 Normal => Vector3.Normalize(NormalUn);
        public Plane Plane => new Plane(Normal, Vector3.Dot(A, Normal));

        public Triangle(Vector3 a, Vector3 b, Vector3 c) => (A, B, C) = (a, b, c);

        public IEnumerable<Vector3> Corners()
        {
            yield return A;
            yield return B;
            yield return C;
        }

        public Vector3 ClosestPoint(Vector3 point)
        {
            var closest = Plane.ClosestPoint(point);
            if (Intersects(closest))
                return closest;

            var closestAB = AB.ClosestPoint(point);
            var closestBC = BC.ClosestPoint(point);
            var closestAC = AC.ClosestPoint(point);
            var distAB = (closestAB - point).LengthSquared();
            var distBC = (closestBC - point).LengthSquared();
            var distAC = (closestAC - point).LengthSquared();
            if (distAB < distBC && distAB < distAC)
                return closestAB;
            if (distBC < distAB && distBC < distAC)
                return closestBC;
            return closestAC;
        }

        public bool Intersects(Vector3 point)
        {
            var (localA, localB, localC) = (A - point, B - point, C - point);
            var normalPAB = Vector3.Cross(localA, localB);
            var normalPBC = Vector3.Cross(localB, localC);
            var normalPCA = Vector3.Cross(localC, localA);
            return
                Vector3.Dot(normalPBC, normalPAB) >= 0 &&
                Vector3.Dot(normalPBC, normalPCA) >= 0;
        }

        public bool Intersects(Box box) => Intersects(box.Corners(), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
        public bool Intersects(Box box, Location boxLoc)
        {
            var (boxRight, boxUp, boxForward) = boxLoc.GlobalRotation.UnitVectors();
            return Intersects(box.Corners(boxLoc), boxRight, boxUp, boxForward);
        }
        public bool Intersects(OrientedBox box)
        {
            var (boxRight, boxUp, boxForward) = box.Orientation.UnitVectors();
            return Intersects(box.Box.Corners(box.Orientation), boxRight, boxUp, boxForward);
        }

        private bool Intersects(IEnumerable<Vector3> boxCorners, Vector3 boxRight, Vector3 boxUp, Vector3 boxForward) =>
            SATIntersects(boxCorners, Corners(), new[]
            {
                boxRight, boxUp, boxForward, Normal,
                Vector3.Cross(boxRight, AB.Vector),
                Vector3.Cross(boxRight, BC.Vector),
                Vector3.Cross(boxRight, CA.Vector),
                Vector3.Cross(boxUp, AB.Vector),
                Vector3.Cross(boxUp, BC.Vector),
                Vector3.Cross(boxUp, CA.Vector),
                Vector3.Cross(boxForward, AB.Vector),
                Vector3.Cross(boxForward, BC.Vector),
                Vector3.Cross(boxForward, CA.Vector),
            });

        public bool Intersects(Triangle other) => SATIntersects(Corners(), other.Corners(), new[]
        {
            Normal, other.Normal,
            SATCrossEdge(AB, other.AB),
            SATCrossEdge(BC, other.AB),
            SATCrossEdge(CA, other.AB),
            SATCrossEdge(AB, other.BC),
            SATCrossEdge(BC, other.BC),
            SATCrossEdge(CA, other.BC),
            SATCrossEdge(AB, other.CA),
            SATCrossEdge(BC, other.CA),
            SATCrossEdge(CA, other.CA),
        });

        public bool Intersects(Plane plane)
        {
            var da = plane.SignedDistanceTo(A);
            var db = plane.SignedDistanceTo(B);
            var dc = plane.SignedDistanceTo(C);
            return
                (CmpZero(da) && CmpZero(db) && CmpZero(dc)) ||
                !(da < 0 && db < 0 && dc < 0) ||
                !(da > 0 && db > 0 && dc > 0);
        }

        public bool Intersects(Sphere sphere) => sphere.Intersects(this);

        public Vector3 Barycentric(Vector3 point)
        {
            if (Sse41.IsSupported)
                return BarycentricSse41(point);
            if (CmpZero(Vector3.Cross(AB.Vector, AC.Vector).LengthSquared()))
                return new Vector3(-1f);
            var AP_Vector = point - A;
            float d00 = Vector3.Dot(AB.Vector, AB.Vector);
            float d01 = Vector3.Dot(AB.Vector, AC.Vector);
            float d11 = Vector3.Dot(AC.Vector, AC.Vector);
            float d20 = Vector3.Dot(AP_Vector, AB.Vector);
            float d21 = Vector3.Dot(AP_Vector, AC.Vector);
            float denom = d00 * d11 - d01 * d01;
            if (CmpZero(denom))
                return new Vector3(-1f); // more obvious than Zero

            Vector3 result;
            result.Y = (d11 * d20 - d01 * d21) / denom;
            result.Z = (d00 * d21 - d01 * d20) / denom;
            result.X = 1f - result.Y - result.Z;
            return result;
        }

        private Vector3 BarycentricSse41(Vector3 point)
        {
            var a = A.AsVector128();
            var b = B.AsVector128();
            var c = C.AsVector128();
            var p = point.AsVector128();
            var ab = Sse.Subtract(b, a);
            var ac = Sse.Subtract(c, a);
            var abxac = CrossProductSse41(ab, ac);
            var abxac_len = Sse41.DotProduct(abxac, abxac, 0b01110001).GetElement(0);
            if (CmpZero(abxac_len))
                return new Vector3(-1f);

            var ap = Sse.Subtract(p, a);
            float d00 = Sse41.DotProduct(ab, ab, 0b01110001).GetElement(0);
            float d01 = Sse41.DotProduct(ab, ac, 0b01110001).GetElement(0);
            float d11 = Sse41.DotProduct(ac, ac, 0b01110001).GetElement(0);
            float d20 = Sse41.DotProduct(ap, ab, 0b01110001).GetElement(0);
            float d21 = Sse41.DotProduct(ap, ac, 0b01110001).GetElement(0);
            float denom = d00 * d11 - d01 * d01;
            if (CmpZero(denom))
                return new Vector3(-1f);

            Vector3 result;
            result.Y = (d11 * d20 - d01 * d21) / denom;
            result.Z = (d00 * d21 - d01 * d20) / denom;
            result.X = 1f - result.Y - result.Z;
            return result;
        }

        private Vector128<float> CrossProductSse41(Vector128<float> a, Vector128<float> b)
        {
            // based on https://geometrian.com/programming/tutorials/cross-product/index.php method 5
            var tmp0 = Sse.Shuffle(a, a, 0b11001001);
            var tmp1 = Sse.Shuffle(b, b, 0b11010010);
            var tmp2 = Sse.Multiply(tmp0, b);
            var tmp3 = Sse.Multiply(tmp0, tmp1);
            var tmp4 = Sse.Shuffle(tmp2, tmp2, 0b11001001);
            return Sse.Subtract(tmp3, tmp4);
        }

        public Raycast? Cast(Ray ray) => ray.Cast(this);
        public Raycast? Cast(Line line) => line.Cast(this);
    }
}
