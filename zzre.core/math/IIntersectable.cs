using System;

namespace zzre
{
    public interface IIntersectable
    {
        public bool Intersects(Box box);
        public bool Intersects(OrientedBox box);
        public bool Intersects(Sphere sphere);
        public bool Intersects(Plane plane);
        public bool Intersects(Triangle triangle);
    }

    public static class IIntersectableExtensions
    {
        public static bool Intersects(this IIntersectable a, IIntersectable b) => Intersects(a, b, true);

        private static bool Intersects(IIntersectable a, IIntersectable b, bool shouldTryToSwitch) => b switch
        {
            Box box             => a.Intersects(box),
            OrientedBox box     => a.Intersects(box),
            Sphere sphere       => a.Intersects(sphere),
            Plane plane         => a.Intersects(plane),
            Triangle triangle   => a.Intersects(triangle),
            _ => shouldTryToSwitch
                ? Intersects(b, a, false)
                : throw new ArgumentException("One of the arguments has to fit the IIntersectable interface")
        };
    }
}
