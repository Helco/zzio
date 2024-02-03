using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace zzre;

public interface IIntersectable<T>
{
    public bool Intersects(in T item);
}

public interface IIntersectionable<T> : IIntersectable<T>
{
    public IEnumerable<Intersection> Intersections(in T item);
    bool IIntersectable<T>.Intersects(in T item) => Intersections(item).Any();
}

public interface IIntersectable :
    IIntersectable<Vector3>,
    IIntersectable<Box>,
    IIntersectable<OrientedBox>,
    IIntersectable<Sphere>,
    IIntersectable<Plane>,
    IIntersectable<Triangle>,
    IIntersectable<Line>
{
    public bool Intersects(IIntersectable other) =>
        IntersectionQueries.Intersects(this, other, true);
}

public interface IIntersectionable :
    IIntersectionable<Box>,
    IIntersectionable<OrientedBox>,
    IIntersectionable<Sphere>,
    IIntersectionable<Triangle>,
    IIntersectionable<Line>
{
}
