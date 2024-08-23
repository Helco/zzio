using System.Runtime.InteropServices;

namespace zzre;

internal unsafe readonly struct AnyIntersectionable
{
    [StructLayout(LayoutKind.Explicit)]
    private struct Storage
    {
        [FieldOffset(0)] public Box box;
        [FieldOffset(0)] public OrientedBox obox;
        [FieldOffset(0)] public Sphere sphere;
        [FieldOffset(0)] public Triangle triangle;
        [FieldOffset(0)] public Line line;
    }
    private readonly Storage storage;
    private readonly delegate*<in Storage, in Plane, PlaneIntersections> sideOf;
    private readonly delegate*<in Storage, in Triangle, Intersection?> intersection;

    public AnyIntersectionable(in Box box)
    {
        storage.box = box;
        sideOf = &BoxSideOf;
        intersection = &BoxIntersection;
    }
    public static implicit operator AnyIntersectionable(in Box box) => new(box);

    public AnyIntersectionable(in OrientedBox obox)
    {
        storage.obox = obox;
        sideOf = &OrientedBoxSideOf;
        intersection = &OrientedBoxIntersection;
    }
    public static implicit operator AnyIntersectionable(in OrientedBox obox) => new(obox);

    public AnyIntersectionable(in Sphere sphere)
    {
        storage.sphere = sphere;
        sideOf = &SphereSideOf;
        intersection = &SphereIntersection;
    }
    public static implicit operator AnyIntersectionable(in Sphere sphere) => new(sphere);

    public AnyIntersectionable(in Triangle triangle)
    {
        storage.triangle = triangle;
        sideOf = &TriangleSideOf;
        intersection = &TriangleIntersection;
    }
    public static implicit operator AnyIntersectionable(in Triangle triangle) => new(triangle);

    public AnyIntersectionable(in Line line)
    {
        storage.line = line;
        sideOf = &LineSideOf;
        intersection = &LineIntersection;
    }
    public static implicit operator AnyIntersectionable(in Line line) => new(line);

    public PlaneIntersections SideOf(in Plane plane) => sideOf(storage, plane);
    public Intersection? Intersect(in Triangle triangle) => intersection(storage, triangle);

    private static PlaneIntersections BoxSideOf(in Storage storage, in Plane plane) =>
        plane.SideOf(storage.box);
    private static Intersection? BoxIntersection(in Storage storage, in Triangle tri) =>
        IntersectionQueries.Intersect(tri, storage.box);

    private static PlaneIntersections OrientedBoxSideOf(in Storage storage, in Plane plane) =>
        plane.SideOf(storage.obox);
    private static Intersection? OrientedBoxIntersection(in Storage storage, in Triangle tri) =>
        IntersectionQueries.Intersect(tri, storage.obox);

    private static PlaneIntersections SphereSideOf(in Storage storage, in Plane plane) =>
        plane.SideOf(storage.sphere);
    private static Intersection? SphereIntersection(in Storage storage, in Triangle tri) =>
        IntersectionQueries.Intersect(tri, storage.sphere);

    private static PlaneIntersections TriangleSideOf(in Storage storage, in Plane plane) =>
        plane.SideOf(storage.triangle);
    private static Intersection? TriangleIntersection(in Storage storage, in Triangle tri) =>
        IntersectionQueries.Intersect(tri, storage.triangle);

    private static PlaneIntersections LineSideOf(in Storage storage, in Plane plane) =>
        plane.SideOf(storage.line);
    private static Intersection? LineIntersection(in Storage storage, in Triangle tri) =>
        IntersectionQueries.Intersect(tri, storage.line);
}
