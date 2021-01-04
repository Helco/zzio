using System;

namespace zzre
{
    public interface IIntersectable
    {
        public bool Intersects(Box box);
        public bool Intersects(OrientedBox box);
        public bool Intersects(Sphere sphere);
        public bool Intersects(Plane plane);
    }
}
