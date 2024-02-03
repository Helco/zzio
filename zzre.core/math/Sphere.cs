using System;
using System.Collections.Generic;
using System.Numerics;

namespace zzre;

public readonly struct Sphere : IRaycastable, IIntersectable
{
    public readonly Vector3 Center;
    public readonly float Radius;
    public float RadiusSq => Radius * Radius;

    public static Sphere Zero = new();

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

    public Sphere TransformToWorld(Location location) => new(
        Vector3.Transform(Center, location.LocalToWorld),
        Radius);

    public bool IsInside(Vector3 point) =>
        Vector3.DistanceSquared(point, Center) <= RadiusSq;

    public bool Intersects(Vector3 point) => IsInside(point);
    public bool Intersects(Sphere other) =>
        (Center - other.Center).LengthSquared() <= MathF.Pow(Radius + other.Radius, 2f);
    public bool Intersects(Box box) => Intersects(box.ClosestPoint(Center));
    public bool Intersects(Box box, Location location) => Intersects(box.ClosestPoint(location, Center));
    public bool Intersects(OrientedBox box) => Intersects(box.Box.ClosestPoint(box.Orientation, Center));
    public bool Intersects(Plane plane) => Intersects(plane.ClosestPoint(Center));
    public bool Intersects(Triangle triangle) => Intersects(triangle.ClosestPoint(Center));
    public bool Intersects(Line line) => Cast(line).HasValue;

    public Raycast? Cast(Ray ray) => ray.Cast(this);
    public Raycast? Cast(Line line) => line.Cast(this);

    public IEnumerable<Line> Edges(int horizontalSections = 8, int verticalSections = 7)
    {
        var horRot = Matrix3x2.CreateRotation(2f * MathF.PI / horizontalSections);
        var verRot = Matrix3x2.CreateRotation(MathF.PI / verticalSections);
        Vector2 curHor, curVer;

        // Vertical edges
        curHor = Vector2.UnitX;
        for (int i = 0; i < horizontalSections; i++)
        {
            curVer = Vector2.UnitY;
            var lastPoint = EdgePoint(curHor, curVer);
            curVer = Vector2.Transform(curVer, verRot);

            for (int j = 0; j < verticalSections; j++)
            {
                var newPoint = EdgePoint(curHor, curVer);
                yield return new Line(lastPoint, newPoint);
                lastPoint = newPoint;
                curVer = Vector2.Transform(curVer, verRot);
            }
            curHor = Vector2.Transform(curHor, horRot);
        }

        // Horizontal edges
        curVer = Vector2.UnitY;
        for (int i = 0; i < verticalSections; i++)
        {
            curHor = Vector2.UnitX;
            var lastPoint = EdgePoint(curHor, curVer);
            curHor = Vector2.Transform(curHor, horRot);

            for (int j = 0; j < horizontalSections; j++)
            {
                var newPoint = EdgePoint(curHor, curVer);
                yield return new Line(lastPoint, newPoint);
                lastPoint = newPoint;
                curHor = Vector2.Transform(curHor, horRot);
            }

            curVer = Vector2.Transform(curVer, verRot);
        }
    }

    private Vector3 EdgePoint(Vector2 hor, Vector2 ver) =>
        Center + new Vector3(hor.X * ver.X, ver.Y, hor.Y * ver.X) * Radius;
}
