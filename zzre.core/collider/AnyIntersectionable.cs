using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using static zzre.MathEx;

namespace zzre;

public unsafe readonly struct AnyIntersectionable
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
    private readonly delegate*<in Storage, int, float, PlaneIntersections> sideOf2;
    private readonly delegate*<in Storage, in Triangle, Intersection?> intersection;

    public static AnyIntersectionable From<T>(in T primitive) where T : struct, IIntersectable => primitive switch
    {
        Box box => box,
        OrientedBox obox => obox,
        Sphere sphere => sphere,
        Triangle triangle => triangle,
        Line line => line,
        _ => throw new System.NotImplementedException()
    };

    public AnyIntersectionable(in Box box)
    {
        storage.box = box;
        sideOf = &BoxSideOf;
        sideOf2 = &BoxSideOf2;
        intersection = &BoxIntersection;
    }
    public static implicit operator AnyIntersectionable(in Box box) => new(box);

    public AnyIntersectionable(in OrientedBox obox)
    {
        storage.obox = obox;
        sideOf = &OrientedBoxSideOf;
        sideOf2 = &OrientedBoxSideOf2;
        intersection = &OrientedBoxIntersection;
    }
    public static implicit operator AnyIntersectionable(in OrientedBox obox) => new(obox);

    public AnyIntersectionable(in Sphere sphere)
    {
        storage.sphere = sphere;
        sideOf = &SphereSideOf;
        sideOf2 = &SphereSideOf2;
        intersection = &SphereIntersection;
    }
    public static implicit operator AnyIntersectionable(in Sphere sphere) => new(sphere);

    public AnyIntersectionable(in Triangle triangle)
    {
        storage.triangle = triangle;
        sideOf = &TriangleSideOf;
        sideOf2 = &TriangleSideOf2;
        intersection = &TriangleIntersection;
    }
    public static implicit operator AnyIntersectionable(in Triangle triangle) => new(triangle);

    public AnyIntersectionable(in Line line)
    {
        storage.line = line;
        sideOf = &LineSideOf;
        sideOf2 = &LineSideOf2;
        intersection = &LineIntersection;
    }
    public static implicit operator AnyIntersectionable(in Line line) => new(line);

    public PlaneIntersections SideOf(in Plane plane) => sideOf(storage, plane);
    public PlaneIntersections SideOf(int planeComponent, float planeValue) => sideOf2(storage, planeComponent, planeValue);
    public Intersection? Intersect(in Triangle triangle) => intersection(storage, triangle);

    [MethodImpl(MIOptions)]
    private static unsafe Plane PlaneFromComponent(int index, float value)
    {
        Vector3 normal = Vector3.Zero;
        ((float*)&normal)[index] = 1.0f;
        return new Plane(normal, value);
    }

    private static PlaneIntersections BoxSideOf(in Storage storage, in Plane plane) =>
        plane.SideOf(storage.box);
    private static PlaneIntersections BoxSideOf2(in Storage storage, int comp, float value) =>
        PlaneFromComponent(comp, value).SideOf(storage.box);
    private static Intersection? BoxIntersection(in Storage storage, in Triangle tri) =>
        IntersectionQueries.Intersect(tri, storage.box);

    private static PlaneIntersections OrientedBoxSideOf(in Storage storage, in Plane plane) =>
        plane.SideOf(storage.obox);
    private static PlaneIntersections OrientedBoxSideOf2(in Storage storage, int comp, float value) =>
        PlaneFromComponent(comp, value).SideOf(storage.obox);
    private static Intersection? OrientedBoxIntersection(in Storage storage, in Triangle tri) =>
        IntersectionQueries.Intersect(tri, storage.obox);

    private static PlaneIntersections SphereSideOf(in Storage storage, in Plane plane) =>
        plane.SideOf(storage.sphere);
    private static PlaneIntersections SphereSideOf2(in Storage storage, int comp, float value)
    {
        float signedDistance = storage.sphere.Center.Component(comp) - value;
        return System.MathF.Abs(signedDistance) <= storage.sphere.Radius
            ? PlaneIntersections.Intersecting
            : signedDistance > 0 ? PlaneIntersections.Inside : PlaneIntersections.Outside;
    }
    private static Intersection? SphereIntersection(in Storage storage, in Triangle tri) =>
        IntersectionQueries.Intersect(tri, storage.sphere);

    private static PlaneIntersections TriangleSideOf(in Storage storage, in Plane plane) =>
        plane.SideOf(storage.triangle);
    private static PlaneIntersections TriangleSideOf2(in Storage storage, int comp, float value) =>
        PlaneFromComponent(comp, value).SideOf(storage.triangle);
    private static Intersection? TriangleIntersection(in Storage storage, in Triangle tri) =>
        IntersectionQueries.Intersect(tri, storage.triangle);

    private static PlaneIntersections LineSideOf(in Storage storage, in Plane plane) =>
        plane.SideOf(storage.line);
    private static PlaneIntersections LineSideOf2(in Storage storage, int comp, float value) =>
        PlaneFromComponent(comp, value).SideOf(storage.line);
    private static Intersection? LineIntersection(in Storage storage, in Triangle tri) =>
        IntersectionQueries.Intersect(tri, storage.line);
}
