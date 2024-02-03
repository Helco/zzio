using System;
using System.Collections.Generic;
using System.Linq;
using zzio;

namespace zzre;

public abstract class BaseGeometryCollider :
    IRaycastable,
    IIntersectionable,
    IIntersectable<Box>,
    IIntersectable<OrientedBox>,
    IIntersectable<Sphere>,
    IIntersectable<Triangle>,
    IIntersectable<Line>
{
    protected abstract IRaycastable CoarseCastable { get; }
    protected abstract IIntersectable CoarseIntersectable { get; }
    public abstract Raycast? Cast(Ray ray, float maxLength);
    protected abstract IEnumerable<Intersection> Intersections<T, TQueries>(T primitive)
        where T : struct, IIntersectable
        where TQueries : IIntersectionQueries<T>;

    public Raycast? Cast(Ray ray) => Cast(ray, float.PositiveInfinity);
    public Raycast? Cast(Line line) => Cast(new Ray(line.Start, line.Direction), line.Length);

    public IEnumerable<Intersection> Intersections(in Box box) => Intersections<Box, IntersectionQueries>(box);
    public IEnumerable<Intersection> Intersections(in OrientedBox box) => Intersections<OrientedBox, IntersectionQueries>(box);
    public IEnumerable<Intersection> Intersections(in Sphere sphere) => Intersections<Sphere, IntersectionQueries>(sphere);
    public IEnumerable<Intersection> Intersections(in Triangle triangle) => Intersections<Triangle, IntersectionQueries>(triangle);
    public IEnumerable<Intersection> Intersections(in Line line) => Intersections<Line, IntersectionQueries>(line);

    public bool Intersects(in Box box) => Intersections(box).Any();
    public bool Intersects(in OrientedBox box) => Intersections(box).Any();
    public bool Intersects(in Sphere sphere) => Intersections(sphere).Any();
    public bool Intersects(in Triangle triangle) => Intersections(triangle).Any();
    public bool Intersects(in Line line) => Intersections(line).Any();
}

public abstract class TriangleCollider : BaseGeometryCollider
{
    protected abstract int TriangleCount { get; }
    public abstract (Triangle Triangle, VertexTriangle VertexTriangle) GetTriangle(int i);
}
