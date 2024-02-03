using System;
using System.Collections.Generic;
using System.Numerics;

namespace zzre;

public partial struct Plane : IRaycastable, IIntersectable
{
    private Vector3 normal;
    public Vector3 Normal
    {
        get => normal;
        set => normal = Math.Abs(value.LengthSquared() - 1.0f) > 0.00001f
            ? Vector3.Normalize(value)
            : value;
    }

    public float Distance { get; set; }

    public Plane(Vector3 normal, float distance)
    {
        this.normal = Vector3.Zero;
        Distance = distance;
        Normal = normal;
    }

    public float SignedDistanceTo(Vector3 point) => Vector3.Dot(point, normal) - Distance;
    public float DistanceTo(Vector3 point) => Math.Abs(SignedDistanceTo(point));
    public int SideOf(Vector3 point) => Math.Sign(SignedDistanceTo(point));
    public Vector3 ClosestPoint(Vector3 point) => point - normal * SignedDistanceTo(point);

    public PlaneIntersections SideOf(Box box) => SideOf(box.Corners());
    public PlaneIntersections SideOf(OrientedBox box) => SideOf(box.AABox.Corners(box.Orientation));
    public PlaneIntersections SideOf(Triangle triangle) => SideOf(triangle.Corners());
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

    public PlaneIntersections SideOf(Sphere sphere)
    {
        var dist = SignedDistanceTo(sphere.Center);
        return MathF.Abs(dist) <= sphere.Radius
            ? PlaneIntersections.Intersecting
            : dist > 0 ? PlaneIntersections.Inside : PlaneIntersections.Outside;
    }

    public PlaneIntersections SideOf(Line line)
    {
        var start = SideOf(line.Start) > 0 ? PlaneIntersections.Inside : PlaneIntersections.Outside;
        var end = SideOf(line.End) > 0 ? PlaneIntersections.Inside : PlaneIntersections.Outside;
        return start | end;
    }
}
