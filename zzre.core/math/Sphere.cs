using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace zzre
{
    public readonly struct Sphere : IRaycastable
    {
        public readonly Vector3 Center;
        public readonly float Radius;
        public float RadiusSq => Radius * Radius;

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

        public Sphere TransformToWorld(Location location) => new Sphere(
            Vector3.Transform(Center, location.LocalToWorld),
            Radius);

        public bool IsInside(Vector3 point) =>
            Vector3.DistanceSquared(point, Center) <= RadiusSq;

        public bool Intersects(Vector3 point) => IsInside(point);
        public bool Intersects(Sphere other) =>
            (Center - other.Center).LengthSquared() <= MathF.Pow(Radius + other.Radius, 2f);
        public bool Intersects(Box box) => Intersects(box.ClosestPoint(Center));
        public bool Intersects(Box box, Location location) => Intersects(box.ClosestPoint(location, Center));
        public bool Intersects(Plane plane) => Intersects(plane.ClosestPoint(Center));
        public bool Intersects(Triangle triangle) => Intersects(triangle.ClosestPoint(Center));

        public Raycast? Cast(Ray ray) => ray.Cast(this);
        public Raycast? Cast(Line line) => line.Cast(this);
    }
}
