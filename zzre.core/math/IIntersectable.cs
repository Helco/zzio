using System.Numerics;

namespace zzre;

public interface IIntersectable<T>
{
    public bool Intersects(in T item);
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
