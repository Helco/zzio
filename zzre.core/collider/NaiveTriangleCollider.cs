using System;
using System.Collections.Generic;
using System.Linq;
using zzio;

namespace zzre;

public abstract class NaiveTriangleCollider : TriangleCollider
{
    private IEnumerable<(Triangle Triangle, VertexTriangle VertexTriangle)> Triangles => Enumerable
        .Range(0, TriangleCount)
        .Select(GetTriangle);

    public override Raycast? Cast(Ray ray, float maxDist) => CoarseCastable.Cast(ray) == null ? null : Triangles
        .Select(t => ray.Cast(t.Triangle, t.VertexTriangle))
        .Where(c => c?.Distance <= maxDist)
        .OrderBy(c => c?.Distance ?? float.PositiveInfinity)
        .FirstOrDefault();

    protected override IEnumerable<Intersection> Intersections<T, TQueries>(T primitive) =>
        CoarseIntersectable.Intersects(primitive)
        ? Triangles
            .Select(t => TQueries.Intersect(t.Triangle, primitive))
            .NotNull()
        : Enumerable.Empty<Intersection>();
}
