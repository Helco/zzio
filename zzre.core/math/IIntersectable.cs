using System;
using System.Collections.Generic;

namespace zzre;

public interface IIntersectable
{
    public bool Intersects(Box box);
    public bool Intersects(OrientedBox box);
    public bool Intersects(Sphere sphere);
    public bool Intersects(Plane plane);
    public bool Intersects(Triangle triangle);
    public bool Intersects(Line line);
}

public interface IIntersectionable : IIntersectable
{
    public IEnumerable<Intersection> Intersections(Box box);
    public IEnumerable<Intersection> Intersections(OrientedBox box);
    public IEnumerable<Intersection> Intersections(Sphere sphere);
    public IEnumerable<Intersection> Intersections(Triangle triangle);
    public IEnumerable<Intersection> Intersections(Line line);
}

public static class IIntersectableExtensions
{
    public static bool Intersects(this IIntersectable a, IIntersectable b) => Intersects(a, b, true);

    private static bool Intersects(IIntersectable a, IIntersectable b, bool shouldTryToSwitch) => b switch
    {
        Box box => a.Intersects(box),
        OrientedBox box => a.Intersects(box),
        Sphere sphere => a.Intersects(sphere),
        Plane plane => a.Intersects(plane),
        Triangle triangle => a.Intersects(triangle),
        Line line when a is IRaycastable raycastable => raycastable.Cast(line).HasValue,
        _ => shouldTryToSwitch
            ? Intersects(b, a, false)
            : throw new ArgumentException("One of the arguments has to fit the IIntersectable interface")
    };
}
