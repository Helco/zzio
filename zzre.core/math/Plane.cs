using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace zzre;

public readonly partial struct Plane : IRaycastable, IIntersectable
{
    public readonly Vector3 Normal;
    public readonly float Distance;

    public Plane(Vector3 normal, float distance)
    {
        Distance = distance;
        Normal = normal;
    }

    [MethodImpl(MathEx.MIOptions)]
    public float SignedDistanceTo(Vector3 point) => Vector3.Dot(point, Normal) - Distance;
    [MethodImpl(MathEx.MIOptions)]
    public float DistanceTo(Vector3 point) => Math.Abs(SignedDistanceTo(point));
    [MethodImpl(MathEx.MIOptions)]
    public int SideOf(Vector3 point) => Math.Sign(SignedDistanceTo(point));
    [MethodImpl(MathEx.MIOptions)]
    public Vector3 ClosestPoint(Vector3 point) => point - Normal * SignedDistanceTo(point);

    // TODO: Do not allocate in Plane.SideOf!

    [MethodImpl(MathEx.MIOptions)]
    public PlaneIntersections SideOf(Box box) => SideOf(box.Corners());
    [MethodImpl(MathEx.MIOptions)]
    public PlaneIntersections SideOf(OrientedBox box) => SideOf(box.AABox.Corners(box.Orientation));
    [MethodImpl(MathEx.MIOptions)]
    public PlaneIntersections SideOf(Triangle triangle) => SideOf(triangle.Corners());
    [MethodImpl(MathEx.MIOptions)]
    private PlaneIntersections SideOf(IEnumerable<Vector3> corners)
    {
        PlaneIntersections intersections = default;
        foreach (var corner in corners)
        {
            intersections |= SideOf(corner) >= 0
                ? PlaneIntersections.Inside
                : PlaneIntersections.Outside;
            if (intersections == PlaneIntersections.Intersecting)
                break;
        }
        return intersections;
    }

    [MethodImpl(MathEx.MIOptions)]
    public PlaneIntersections SideOf(Sphere sphere)
    {
        var dist = SignedDistanceTo(sphere.Center);
        return MathF.Abs(dist) <= sphere.Radius
            ? PlaneIntersections.Intersecting
            : dist > 0 ? PlaneIntersections.Inside : PlaneIntersections.Outside;
    }

    [MethodImpl(MathEx.MIOptions)]
    public PlaneIntersections SideOf(Line line)
    {
        var start = SideOf(line.Start) > 0 ? PlaneIntersections.Inside : PlaneIntersections.Outside;
        var end = SideOf(line.End) > 0 ? PlaneIntersections.Inside : PlaneIntersections.Outside;
        return start | end;
    }
}
