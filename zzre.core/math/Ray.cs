using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using zzio.rwbs;

namespace zzre;

public readonly struct Ray
{
    public readonly Vector3 Start;
    public readonly Vector3 Direction;

    public Ray(Vector3 start, Vector3 dir) => (Start, Direction) = (start, Vector3.Normalize(dir));

    [MethodImpl(MathEx.MIOptions)]
    public bool Intersects(Vector3 point) => MathEx.Cmp(1f, Vector3.Dot(Direction, Vector3.Normalize(point - Start)));
    [MethodImpl(MathEx.MIOptions)]
    public float PhaseOf(Vector3 point) => Vector3.Dot(point - Start, Direction);
    [MethodImpl(MathEx.MIOptions)]
    public Vector3 ClosestPoint(Vector3 point) => Start + Direction * Math.Max(0f, PhaseOf(point));

    [MethodImpl(MathEx.MIOptions)]
    public Raycast? Cast(Sphere sphere)
    {
        var e = sphere.Center - Start;
        var eSq = e.LengthSquared();
        var a = Vector3.Dot(e, Direction);
        var f = MathF.Sqrt(sphere.RadiusSq - (eSq - a * a));
        if (!float.IsFinite(f))
            return null;

        var d = eSq < sphere.RadiusSq ? a + f : a - f;
        if (d < 0.0f)
            return null;
        var p = Start + Direction * d;
        var n = Vector3.Normalize(p - sphere.Center);
        return new Raycast(d, p, n);
    }

    [MethodImpl(MathEx.MIOptions)]
    public Raycast? Cast(Box box)
    {
        return Cast(AABBSlabPoints(box, this));

        static IEnumerable<((float t, Vector3 n), (float t, Vector3 n))> AABBSlabPoints(Box box, Ray me)
        {
            if (!MathEx.CmpZero(me.Direction.X))
                yield return (
                    ((box.Min.X - me.Start.X) / me.Direction.X, -Vector3.UnitX),
                    ((box.Max.X - me.Start.X) / me.Direction.X, Vector3.UnitX));
            if (!MathEx.CmpZero(me.Direction.Y))
                yield return (
                    ((box.Min.Y - me.Start.Y) / me.Direction.Y, -Vector3.UnitY),
                    ((box.Max.Y - me.Start.Y) / me.Direction.Y, Vector3.UnitY));
            if (!MathEx.CmpZero(me.Direction.Z))
                yield return (
                    ((box.Min.Z - me.Start.Z) / me.Direction.Z, -Vector3.UnitZ),
                    ((box.Max.Z - me.Start.Z) / me.Direction.Z, Vector3.UnitZ));
        }
    }

    [MethodImpl(MathEx.MIOptions)]
    public Raycast? Cast(OrientedBox obb)
    {
        [MethodImpl(MathEx.MIOptions)]
        static ((float t, Vector3 n), (float t, Vector3 n))? OBBSlabPoint(Box box, Ray me, Vector3 axis, float halfSize)
        {
            float dirOnAxis = Vector3.Dot(axis, me.Direction);
            float posOnAxis = Vector3.Dot(axis, box.Center - me.Start);
            if (MathEx.CmpZero(dirOnAxis))
            {
                if (-posOnAxis - halfSize > 0 ||
                    -posOnAxis + halfSize < 0)
                    return null;
                dirOnAxis = 0.00001f;
            }

            return (
                ((posOnAxis + halfSize) / dirOnAxis, axis),
                ((posOnAxis - halfSize) / dirOnAxis, -axis));
        }

        var invalid = ((-1f, Vector3.Zero), (-1f, Vector3.Zero));
        var (box, boxRot) = obb;
        var (boxRight, boxUp, boxForward) = boxRot.UnitVectors();
        return Cast(new[]
        {
            OBBSlabPoint(box, this, boxRight, box.HalfSize.X) ?? invalid,
            OBBSlabPoint(box, this, boxUp, box.HalfSize.Y) ?? invalid,
            OBBSlabPoint(box, this, boxForward, box.HalfSize.Z) ?? invalid,
        });
    }

    [MethodImpl(MathEx.MIOptions)]
    private Raycast? Cast(IEnumerable<((float t, Vector3 n), (float t, Vector3 n))> slabPoints)
    {
        var tmin = (t: float.MinValue, n: Vector3.Zero);
        var tmax = (t: float.MaxValue, n: Vector3.Zero);

        foreach (var cur in slabPoints)
        {
            var (curMin, curMax) = (cur.Item1, cur.Item2);
            if (curMax.t < curMin.t)
                (curMin, curMax) = (curMax, curMin);
            if (curMin.t > tmin.t)
                tmin = curMin;
            if (curMax.t < tmax.t)
                tmax = curMax;
        }

        if (tmax.t < 0 || tmin.t > tmax.t)
            return null;
        var t = tmin.t < 0 ? tmax : tmin;
        var p = Start + Direction * t.t;
        return new Raycast(t.t, p, t.n);
    }

    [MethodImpl(MathEx.MIOptions)]
    public Raycast? Cast(Plane plane)
    {
        float angle = Vector3.Dot(Direction, plane.Normal);
        float rayPos = Vector3.Dot(Start, plane.Normal);
        float t = (plane.Distance - rayPos) / angle;
        if (angle >= 0.0f || float.IsNaN(t) || t < 0)
            return null;

        return new Raycast(t, Start + Direction * t, plane.Normal);
    }

    [MethodImpl(MathEx.MIOptions)]
    public float? DistanceTo(CollisionSectorType sectorType, float planeDistance) =>
        DistanceToUnitPlane(sectorType.ToIndex(), planeDistance);
    [MethodImpl(MathEx.MIOptions)]
    public float? DistanceTo(RWPlaneSectionType sectionType, float planeDistance) =>
        DistanceToUnitPlane(sectionType.ToIndex(), planeDistance);
    [MethodImpl(MathEx.MIOptions)]
    private float? DistanceToUnitPlane(int componentIndex, float planeDistance)
    {
        float angle = Direction.Component(componentIndex);
        float rayPos = Start.Component(componentIndex);
        float t = (planeDistance - rayPos) / angle;
        return angle >= 0.0f || float.IsNaN(t) || t < 0 ? null : t;
    }

    [MethodImpl(MathEx.MIOptions)]
    public Raycast? Cast(Triangle triangle, WorldTriangleId? triangleId = null)
    {
        if (triangle.IsDegenerated)
            return null;
        var cast = Cast(triangle.Plane);
        if (cast == null)
            return null;
        var bary = triangle.Barycentric(cast.Value.Point);
        return bary.X >= 0.0f && bary.X <= 1.0f &&
            bary.Y >= 0.0f && bary.Y <= 1.0f &&
            bary.Z >= 0.0f && bary.Z <= 1.0f
            ? cast.Value with { TriangleId = triangleId } : null;
    }
}
