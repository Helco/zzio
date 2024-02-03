using System;
using System.Numerics;

namespace zzre;

partial class IntersectionQueries
{
    // Type-erased version
    internal static bool Intersects(IIntersectable a, IIntersectable b, bool shouldTryToSwitch) => (a, b) switch
    {
        { a: Triangle A, b: Box B } => Intersects(A, B),
        { a: Triangle A, b: OrientedBox B } => Intersects(A, B),
        { a: Triangle A, b: Sphere B } => Intersects(B, A),
        { a: Triangle A, b: Triangle B } => Intersects(A, B),
        { a: Triangle A, b: Plane B } => Intersects(A, B),
        { a: Box A, b: Box B } => Intersects(A, B),
        { a: Box A, b: OrientedBox B } => Intersects(A, B),
        { a: Box A, b: Sphere B } => Intersects(B, A),
        { a: Box A, b: Plane B } => Intersects(A, B),
        { a: OrientedBox A, b: OrientedBox B } => Intersects(A, B),
        { a: OrientedBox A, b: Sphere B } => Intersects(B, A),
        { a: OrientedBox A, b: Plane B } => Intersects(A, B),
        { a: Sphere A, b: Sphere B } => Intersects(A, B),
        { a: Sphere A, b: Plane B } => Intersects(A, B),
        { a: Plane A, b: Plane B } => Intersects(A, B),
        { a: IRaycastable raycastable, b: Line line } => raycastable.Cast(line).HasValue,
        _ when shouldTryToSwitch => Intersects(b, a, false),
        _ => throw new ArgumentException($"Intersection between {a.GetType().Name} and {b.GetType().Name} is missing")
    };
}

partial struct Box
{
    public bool Intersects(in Vector3 point) => IntersectionQueries.Intersects(this, point);
    public bool Intersects(Location boxLoc, in Vector3 point) => IntersectionQueries.Intersects(this, boxLoc, point);
    public bool Intersects(in Box b) => IntersectionQueries.Intersects(this, b);
    public bool Intersects(in OrientedBox b) => IntersectionQueries.Intersects(this, b);
    public bool Intersects(in Sphere b) => IntersectionQueries.Intersects(b, this);
    public bool Intersects(in Plane b) => IntersectionQueries.Intersects(this, b);
    public bool Intersects(in Triangle b) => IntersectionQueries.Intersects(b, this);
    public bool Intersects(in Line b) => b.Cast(this).HasValue;
    public Raycast? Cast(Ray ray) => ray.Cast(this);
    public Raycast? Cast(Line line) => line.Cast(this);
}

partial struct OrientedBox
{
    public bool Intersects(in Vector3 point) => throw new NotImplementedException("OBB-Line intersection is missing");
    public bool Intersects(in Box b) => IntersectionQueries.Intersects(this, b);
    public bool Intersects(in OrientedBox b) => IntersectionQueries.Intersects(this, b);
    public bool Intersects(in Sphere b) => IntersectionQueries.Intersects(b, this);
    public bool Intersects(in Plane b) => IntersectionQueries.Intersects(this, b);
    public bool Intersects(in Triangle b) => IntersectionQueries.Intersects(b, this);
    public bool Intersects(in Line b) => b.Cast(this).HasValue;
    public Raycast? Cast(Ray ray) => ray.Cast(this);
    public Raycast? Cast(Line line) => line.Cast(this);
}

partial struct Sphere
{
    public bool Intersects(in Vector3 point) => IntersectionQueries.Intersects(this, point);
    public bool Intersects(in Box b) => IntersectionQueries.Intersects(this, b);
    public bool Intersects(in OrientedBox b) => IntersectionQueries.Intersects(this, b);
    public bool Intersects(in Sphere b) => IntersectionQueries.Intersects(b, this);
    public bool Intersects(in Plane b) => IntersectionQueries.Intersects(this, b);
    public bool Intersects(in Triangle b) => IntersectionQueries.Intersects(this, b);
    public bool Intersects(in Line b) => b.Cast(this).HasValue;
    public Raycast? Cast(Ray ray) => ray.Cast(this);
    public Raycast? Cast(Line line) => line.Cast(this);
}

partial struct Plane
{
    public bool Intersects(in Vector3 point) => IntersectionQueries.Intersects(this, point);
    public bool Intersects(in Box b) => IntersectionQueries.Intersects(b, this);
    public bool Intersects(in OrientedBox b) => IntersectionQueries.Intersects(b, this);
    public bool Intersects(in Sphere b) => IntersectionQueries.Intersects(b, this);
    public bool Intersects(in Plane b) => IntersectionQueries.Intersects(this, b);
    public bool Intersects(in Triangle b) => IntersectionQueries.Intersects(b, this);
    public bool Intersects(in Line b) => b.Cast(this).HasValue;
    public Raycast? Cast(Ray ray) => ray.Cast(this);
    public Raycast? Cast(Line line) => line.Cast(this);
}

partial struct Triangle
{
    public bool Intersects(in Vector3 point) => IntersectionQueries.Intersects(this, point);
    public bool Intersects(in Box b) => IntersectionQueries.Intersects(this, b);
    public bool Intersects(in OrientedBox b) => IntersectionQueries.Intersects(this, b);
    public bool Intersects(in Sphere b) => IntersectionQueries.Intersects(b, this);
    public bool Intersects(in Plane b) => IntersectionQueries.Intersects(this, b);
    public bool Intersects(in Triangle b) => IntersectionQueries.Intersects(b, this);
    public bool Intersects(in Line b) => b.Cast(this).HasValue;
    public Raycast? Cast(Ray ray) => ray.Cast(this);
    public Raycast? Cast(Line line) => line.Cast(this);
}

partial struct Line
{
    public bool Intersects(in Vector3 point) => IntersectionQueries.Intersects(this, point);
    public bool Intersects(in Box b) => Cast(b).HasValue;
    public bool Intersects(in OrientedBox b) => Cast(b).HasValue;
    public bool Intersects(in Sphere b) => Cast(b).HasValue;
    public bool Intersects(in Plane b) => Cast(b).HasValue;
    public bool Intersects(in Triangle b) => Cast(b).HasValue;
    public bool Intersects(in Line b) => throw new NotImplementedException("Line-line intersection is missing");
}
