using System.Numerics;

namespace zzre
{
    public readonly struct Intersection
    {
        public readonly Vector3 Point;
        public readonly Triangle Triangle;
        public Vector3 Normal => Triangle.Normal;

        public Intersection(Vector3 p, Triangle t) => (Point, Triangle) = (p, t);
    }
}
