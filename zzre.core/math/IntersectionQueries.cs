﻿using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using static zzre.MathEx;

namespace zzre;

public interface IIntersectionQueries<T> where T : struct
{
    static abstract PlaneIntersections SideOf(in Plane plane, in T primitive);
    static abstract PlaneIntersections SideOf(int planeComponent, float planeValue, in T primitive);
    static abstract Intersection? Intersect(in Triangle triangle, in T primitive);
    static abstract T TransformToLocal(in T primitive, Location location);
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

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Box box, Vector3 point) =>
        point.X >= box.Min.X && point.X < box.Max.X &&
        point.Y >= box.Min.Y && point.Y < box.Max.Y &&
        point.Z >= box.Min.Z && point.Z < box.Max.Z;

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Box box, Location boxLoc, Vector3 point) =>
        Intersects(box, Vector3.Transform(point, boxLoc.WorldToLocal));

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Box a, in Box b) =>
        a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
        a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
        a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Box a, in OrientedBox b)
    {
        var (otherR, otherU, otherF) = b.Orientation.UnitVectors();
        return SATIntersects(a.Corners(), b.Corners(),
            new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ },
            new[] { otherR, otherU, otherF });
    }

    [MethodImpl(MIOptions)]
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

    [MethodImpl(MIOptions)]
    public static bool Intersects(in OrientedBox a, in OrientedBox b)
    {
        var (meR, meU, meF) = a.Orientation.UnitVectors();
        var (otherR, otherU, otherF) = b.Orientation.UnitVectors();
        return SATIntersects(
            a.Corners(), b.Corners(),
            new[] { meR, meU, meF },
            new[] { otherR, otherU, otherF });
    }

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Box b, Plane plane) =>
        b.IntervalOn(plane.Normal).Intersects(plane.Distance);

    [MethodImpl(MIOptions)]
    public static bool Intersects(in OrientedBox b, Plane plane) =>
        b.AABox.IntervalOn(b.Orientation, plane.Normal).Intersects(plane.Distance);

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Plane a, in Plane b) =>
        !CmpZero(Vector3.Cross(a.Normal, b.Normal).LengthSquared());

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Plane plane, in Line line) =>
        plane.SideOf(line) == PlaneIntersections.Intersecting;

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Plane plane, Vector3 point) =>
        CmpZero(plane.DistanceTo(point));

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Sphere sphere, Vector3 point) =>
        Vector3.DistanceSquared(point, sphere.Center) <= sphere.RadiusSq;

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Sphere a, in Sphere b) =>
        (a.Center - b.Center).LengthSquared() <= MathF.Pow(a.Radius + b.Radius, 2f);

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Sphere sphere, in Box box) =>
        Intersects(sphere, box.ClosestPoint(sphere.Center));

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Sphere sphere, in Box box, Location boxLoc) =>
        Intersects(sphere, box.ClosestPoint(boxLoc, sphere.Center));

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Sphere sphere, in OrientedBox b) =>
        Intersects(sphere, b.AABox.ClosestPoint(b.Orientation, sphere.Center));

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Sphere sphere, in Plane plane) =>
        Intersects(sphere, plane.ClosestPoint(sphere.Center));

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Sphere sphere, in Triangle triangle) =>
        Intersects(sphere, triangle.ClosestPoint(sphere.Center));

    [MethodImpl(MIOptions)]
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

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Triangle triangle, in Box box) =>
        Intersects(triangle, box.Corners(), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Triangle triangle, in Box box, Location boxLoc)
    {
        var (boxRight, boxUp, boxForward) = boxLoc.GlobalRotation.UnitVectors();
        return Intersects(triangle, box.Corners(boxLoc), boxRight, boxUp, boxForward);
    }

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Triangle triangle, in OrientedBox box)
    {
        var (boxRight, boxUp, boxForward) = box.Orientation.UnitVectors();
        return Intersects(triangle, box.AABox.Corners(box.Orientation), boxRight, boxUp, boxForward);
    }

    [MethodImpl(MIOptions)]
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

    [MethodImpl(MIOptions)]
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

    [MethodImpl(MIOptions)]
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

    [MethodImpl(MIOptions)]
    public static bool Intersects(in Line line, Vector3 point) =>
         CmpZero((point - line.ClosestPoint(point)).LengthSquared());

    // There might be faster methods than this, but it will suffice for now

    [MethodImpl(MIOptions)]
    public static PlaneIntersections SideOf(in Plane plane, in Box box) =>
        plane.SideOf(box);

    [MethodImpl(MIOptions)]
    public static Intersection? Intersect(in Triangle triangle, in Box primitive) =>
        Intersects(triangle, primitive)
        ? new Intersection(triangle.ClosestPoint(primitive.Center), triangle)
        : null;

    [MethodImpl(MIOptions)]
    private static unsafe Plane PlaneFromComponent(int index, float value)
    {
        Vector3 normal = Vector3.Zero;
        ((float*)&normal)[index] = 1.0f;
        return new Plane(normal, value);
    }
    [MethodImpl(MIOptions)]
    public static PlaneIntersections SideOf(int planeComponent, float planeValue, in Box box) =>
        SideOf(PlaneFromComponent(planeComponent, planeValue), box);
    [MethodImpl(MIOptions)]
    public static PlaneIntersections SideOf(int planeComponent, float planeValue, in OrientedBox box) =>
        SideOf(PlaneFromComponent(planeComponent, planeValue), box);
    [MethodImpl(MIOptions)]
    public static PlaneIntersections SideOf(int planeComponent, float planeValue, in Triangle box) =>
        SideOf(PlaneFromComponent(planeComponent, planeValue), box);
    [MethodImpl(MIOptions)]
    public static PlaneIntersections SideOf(int planeComponent, float planeValue, in Line box) =>
        SideOf(PlaneFromComponent(planeComponent, planeValue), box);

    [MethodImpl(MIOptions)]
    public static unsafe PlaneIntersections SideOf(int planeComponent, float planeValue, in Sphere sphere)
    {
        float signedDistance = sphere.Center.Component(planeComponent) - planeValue;
        return MathF.Abs(signedDistance) <= sphere.Radius
            ? PlaneIntersections.Intersecting
            : signedDistance > 0 ? PlaneIntersections.Inside : PlaneIntersections.Outside;
    }


    [MethodImpl(MIOptions)]
    public static PlaneIntersections SideOf(in Plane plane, in Triangle triangle) =>
        plane.SideOf(triangle);
    [MethodImpl(MIOptions)]
    public static Intersection? Intersect(in Triangle triangle, in Triangle primitive) =>
        Intersects(triangle, primitive)
        ? new Intersection(triangle.ClosestPoint((primitive.A + primitive.B + primitive.C) / 3f), triangle)
        : null;

    [MethodImpl(MIOptions)]
    public static PlaneIntersections SideOf(in Plane plane, in OrientedBox box) =>
        plane.SideOf(box);
    [MethodImpl(MIOptions)]
    public static Intersection? Intersect(in Triangle triangle, in OrientedBox primitive) =>
        Intersects(triangle, primitive)
        ? new Intersection(triangle.ClosestPoint(primitive.AABox.Center), triangle)
        : null;

    [MethodImpl(MIOptions)]
    public static PlaneIntersections SideOf(in Plane plane, in Sphere sphere) =>
        plane.SideOf(sphere);
    [MethodImpl(MIOptions)]
    public static Intersection? Intersect(in Triangle triangle, in Sphere primitive) =>
        Intersects(primitive, triangle)
        ? new Intersection(triangle.ClosestPoint(primitive.Center), triangle)
        : null;

    [MethodImpl(MIOptions)]
    public static PlaneIntersections SideOf(in Plane plane, in Line line) =>
        plane.SideOf(line);
    [MethodImpl(MIOptions)]
    public static Intersection? Intersect(in Triangle triangle, in Line line) =>
        triangle.Cast(line)?.AsIntersection(triangle);

    [MethodImpl(MIOptions)]
    public static Box TransformToLocal(in Box box, Location location)
    {
        return new(Vector3.Transform(box.Center, location.WorldToLocal), box.Size);
    }

    [MethodImpl(MIOptions)]
    public static OrientedBox TransformToLocal(in OrientedBox box, Location location)
    {
        var worldToLocal = location.WorldToLocal;
        return new(
            new(Vector3.Transform(box.AABox.Center, worldToLocal), box.AABox.Size),
            box.Orientation * Quaternion.CreateFromRotationMatrix(worldToLocal));
    }

    [MethodImpl(MIOptions)]
    public static Triangle TransformToLocal(in Triangle tri, Location location)
    {
        var worldToLocal = location.WorldToLocal;
        return new(
            Vector3.Transform(tri.A, worldToLocal),
            Vector3.Transform(tri.B, worldToLocal),
            Vector3.Transform(tri.C, worldToLocal));
    }

    [MethodImpl(MIOptions)]
    public static Line TransformToLocal(in Line line, Location location)
    {
        var worldToLocal = location.WorldToLocal;
        return new(
            Vector3.Transform(line.Start, worldToLocal),
            Vector3.Transform(line.End, worldToLocal));
    }

    [MethodImpl(MIOptions)]
    public static Sphere TransformToLocal(in Sphere sphere, Location location)
    {
        var worldToLocal = location.WorldToLocal;
        return new(Vector3.Transform(sphere.Center, worldToLocal), sphere.Radius);
    }
}
