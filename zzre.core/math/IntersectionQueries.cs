using System;
using System.Collections.Generic;
using System.Numerics;
using static zzre.MathEx;

namespace zzre;

public interface IIntersectionQueries<T> where T : struct
{
    static abstract PlaneIntersections SideOf(in Plane plane, in T primitive);
    static abstract Intersection? Intersect(in Triangle triangle, in T primitive);
    static abstract IEnumerable<Intersection> Intersections(BaseGeometryCollider collider, in T primitive);
}

public sealed partial class IntersectionQueries :
    IIntersectionQueries<Box>,
    IIntersectionQueries<OrientedBox>,
    IIntersectionQueries<Triangle>,
    IIntersectionQueries<Sphere>,
    IIntersectionQueries<Line>
{
    // C# oddity, this class works as a static class but cannot be one
    // as then it cannot implement the "static" interface IIntersectionQueries
    private IntersectionQueries() { }

    public static bool Intersects(in Box box, Vector3 point) =>
        point.X >= box.Min.X && point.X < box.Max.X &&
        point.Y >= box.Min.Y && point.Y < box.Max.Y &&
        point.Z >= box.Min.Z && point.Z < box.Max.Z;

    public static bool Intersects(in Box box, Location boxLoc, Vector3 point) =>
        Intersects(box, Vector3.Transform(point, boxLoc.WorldToLocal));

    public static bool Intersects(in Box a, in Box b) =>
        a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
        a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
        a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;

    public static bool Intersects(in Box a, in OrientedBox b)
    {
        var (otherR, otherU, otherF) = b.Orientation.UnitVectors();
        return SATIntersects(a.Corners(), b.Corners(),
            new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ },
            new[] { otherR, otherU, otherF });
    }

    public static bool Intersects(in Box a, Location aLoc, in Box b, Location bLoc)
    {
        throw new InvalidOperationException("Check this situation as an old implementation could have never worked");
#pragma warning disable CS0162 // Unreachable code detected
        var (meTransformed, meRot) = a.TransformToWorld(aLoc);
        var (meR, meU, meF) = meRot.UnitVectors();
        var (otherTransformed, otherRot) = b.TransformToWorld(bLoc);
        var (otherR, otherU, otherF) = otherRot.UnitVectors();
        return SATIntersects(meTransformed.Corners(meRot), otherTransformed.Corners(otherRot),
            new[] { meR, meU, meF }, new[] { otherR, otherU, otherF });
#pragma warning restore CS0162 // Unreachable code detected
    }

    public static bool Intersects(in OrientedBox a, in OrientedBox b)
    {
        var (meR, meU, meF) = a.Orientation.UnitVectors();
        var (otherR, otherU, otherF) = b.Orientation.UnitVectors();
        return SATIntersects(
            a.Corners(), b.Corners(),
            new[] { meR, meU, meF },
            new[] { otherR, otherU, otherF });
    }

    public static bool Intersects(in Box b, Plane plane) =>
        b.IntervalOn(plane.Normal).Intersects(plane.Distance);

    public static bool Intersects(in OrientedBox b, Plane plane) =>
        b.AABox.IntervalOn(b.Orientation, plane.Normal).Intersects(plane.Distance);

    public static bool Intersects(in Plane a, in Plane b) =>
        !CmpZero(Vector3.Cross(a.Normal, b.Normal).LengthSquared());

    public static bool Intersects(in Plane plane, in Line line) =>
        plane.SideOf(line) == PlaneIntersections.Intersecting;

    public static bool Intersects(in Plane plane, Vector3 point) =>
        CmpZero(plane.DistanceTo(point));

    public static bool Intersects(in Sphere sphere, Vector3 point) =>
        Vector3.DistanceSquared(point, sphere.Center) <= sphere.RadiusSq;

    public static bool Intersects(in Sphere a, in Sphere b) =>
        (a.Center - b.Center).LengthSquared() <= MathF.Pow(a.Radius + b.Radius, 2f);

    public static bool Intersects(in Sphere sphere, in Box box) =>
        Intersects(sphere, box.ClosestPoint(sphere.Center));

    public static bool Intersects(in Sphere sphere, in Box box, Location boxLoc) =>
        Intersects(sphere, box.ClosestPoint(boxLoc, sphere.Center));

    public static bool Intersects(in Sphere sphere, in OrientedBox b) =>
        Intersects(sphere, b.AABox.ClosestPoint(b.Orientation, sphere.Center));

    public static bool Intersects(in Sphere sphere, in Plane plane) =>
        Intersects(sphere, plane.ClosestPoint(sphere.Center));

    public static bool Intersects(in Sphere sphere, in Triangle triangle) =>
        Intersects(sphere, triangle.ClosestPoint(sphere.Center));

    public static bool Intersects(in Triangle tri, Vector3 point)
    {
        var (localA, localB, localC) = (tri.A - point, tri.B - point, tri.C - point);
        var normalPAB = Vector3.Cross(localA, localB);
        var normalPBC = Vector3.Cross(localB, localC);
        var normalPCA = Vector3.Cross(localC, localA);
        return
            Vector3.Dot(normalPBC, normalPAB) >= 0 &&
            Vector3.Dot(normalPBC, normalPCA) >= 0;
    }

    public static bool Intersects(in Triangle triangle, in Box box) =>
        Intersects(triangle, box.Corners(), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

    public static bool Intersects(in Triangle triangle, in Box box, Location boxLoc)
    {
        var (boxRight, boxUp, boxForward) = boxLoc.GlobalRotation.UnitVectors();
        return Intersects(triangle, box.Corners(boxLoc), boxRight, boxUp, boxForward);
    }

    public static bool Intersects(in Triangle triangle, in OrientedBox box)
    {
        var (boxRight, boxUp, boxForward) = box.Orientation.UnitVectors();
        return Intersects(triangle, box.AABox.Corners(box.Orientation), boxRight, boxUp, boxForward);
    }

    private static bool Intersects(Triangle tri, IEnumerable<Vector3> boxCorners, Vector3 boxRight, Vector3 boxUp, Vector3 boxForward) =>
        SATIntersects(boxCorners, tri.Corners(), new[]
        {
            boxRight, boxUp, boxForward, tri.Normal,
            Vector3.Cross(boxRight, tri.AB.Vector),
            Vector3.Cross(boxRight, tri.BC.Vector),
            Vector3.Cross(boxRight, tri.CA.Vector),
            Vector3.Cross(boxUp, tri.AB.Vector),
            Vector3.Cross(boxUp, tri.BC.Vector),
            Vector3.Cross(boxUp, tri.CA.Vector),
            Vector3.Cross(boxForward, tri.AB.Vector),
            Vector3.Cross(boxForward, tri.BC.Vector),
            Vector3.Cross(boxForward, tri.CA.Vector),
        });

    public static bool Intersects(in Triangle a, in Triangle b) =>
        SATIntersects(a.Corners(), b.Corners(), new[]
        {
            a.Normal, b.Normal,
            SATCrossEdge(a.AB, b.AB),
            SATCrossEdge(a.BC, b.AB),
            SATCrossEdge(a.CA, b.AB),
            SATCrossEdge(a.AB, b.BC),
            SATCrossEdge(a.BC, b.BC),
            SATCrossEdge(a.CA, b.BC),
            SATCrossEdge(a.AB, b.CA),
            SATCrossEdge(a.BC, b.CA),
            SATCrossEdge(a.CA, b.CA),
        });

    public static bool Intersects(in Triangle tri, in Plane plane)
    {
        var da = plane.SignedDistanceTo(tri.A);
        var db = plane.SignedDistanceTo(tri.B);
        var dc = plane.SignedDistanceTo(tri.C);

        var liesOnPlane = CmpZero(da) && CmpZero(db) && CmpZero(dc);
        var completlyOnLeft = da < 0 && db < 0 && dc < 0;
        var completlyOnRight = da > 0 && db > 0 && dc > 0;
        return liesOnPlane || !completlyOnLeft || !completlyOnRight;
    }

    public static bool Intersects(in Line line, Vector3 point) =>
         CmpZero((point - line.ClosestPoint(point)).LengthSquared());

    // There might be faster methods than this, but it will suffice for now

    public static PlaneIntersections SideOf(in Plane plane, in Box box) =>
        plane.SideOf(box);
    public static Intersection? Intersect(in Triangle triangle, in Box primitive) =>
        Intersects(triangle, primitive)
        ? new Intersection(triangle.ClosestPoint(primitive.Center), triangle)
        : null;
    public static IEnumerable<Intersection> Intersections(BaseGeometryCollider collider, in Box box) =>
        collider.Intersections(box);

    public static PlaneIntersections SideOf(in Plane plane, in Triangle triangle) =>
        plane.SideOf(triangle);
    public static Intersection? Intersect(in Triangle triangle, in Triangle primitive) =>
        Intersects(triangle, primitive)
        ? new Intersection(triangle.ClosestPoint((primitive.A + primitive.B + primitive.C) / 3f), triangle)
        : null;
    public static IEnumerable<Intersection> Intersections(BaseGeometryCollider collider, in Triangle triangle) =>
        collider.Intersections(triangle);

    public static PlaneIntersections SideOf(in Plane plane, in OrientedBox box) =>
        plane.SideOf(box);
    public static Intersection? Intersect(in Triangle triangle, in OrientedBox primitive) =>
        Intersects(triangle, primitive)
        ? new Intersection(triangle.ClosestPoint(primitive.AABox.Center), triangle)
        : null;
    public static IEnumerable<Intersection> Intersections(BaseGeometryCollider collider, in OrientedBox box) =>
        collider.Intersections(box);

    public static PlaneIntersections SideOf(in Plane plane, in Sphere sphere) =>
        plane.SideOf(sphere);
    public static Intersection? Intersect(in Triangle triangle, in Sphere primitive) =>
        Intersects(primitive, triangle)
        ? new Intersection(triangle.ClosestPoint(primitive.Center), triangle)
        : null;
    public static IEnumerable<Intersection> Intersections(BaseGeometryCollider collider, in Sphere sphere) =>
        collider.Intersections(sphere);

    public static PlaneIntersections SideOf(in Plane plane, in Line line) =>
        plane.SideOf(line);
    public static Intersection? Intersect(in Triangle triangle, in Line line) =>
        triangle.Cast(line)?.AsIntersection(triangle);
    public static IEnumerable<Intersection> Intersections(BaseGeometryCollider collider, in Line line) =>
        collider.Intersections(line);


}
