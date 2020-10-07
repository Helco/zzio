using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace zzre
{
    public struct Sphere
    {
        public Vector3 Center;
        public float Radius;

        public static Sphere Zero = new Sphere();

        public Sphere(float x, float y, float z, float r)
        {
            Center = new Vector3(x, y, z);
            Radius = r;
        }

        public Sphere(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        public bool IsInside(Vector3 point) =>
            Vector3.DistanceSquared(point, Center) <= Radius * Radius;
    }
}
