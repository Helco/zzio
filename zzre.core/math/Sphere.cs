using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace zzre;

public readonly partial struct Sphere : IRaycastable, IIntersectable
{
    public readonly Vector3 Center;
    public readonly float Radius;
    public float RadiusSq { [MethodImpl(MathEx.MIOptions)]
        get => Radius * Radius; }

    public static readonly Sphere Zero;

    [MethodImpl(MathEx.MIOptions)]
    public Sphere(float x, float y, float z, float r)
    {
        Center = new Vector3(x, y, z);
        Radius = r;
    }

    [MethodImpl(MathEx.MIOptions)]
    public Sphere(Vector3 center, float radius)
    {
        Center = center;
        Radius = radius;
    }

    [MethodImpl(MathEx.MIOptions)]
    public Sphere TransformToWorld(Location location) => new(
        Vector3.Transform(Center, location.LocalToWorld),
        Radius);

    // TODO: Remove allocations in Sphere
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

    [MethodImpl(MathEx.MIOptions)]
    private Vector3 EdgePoint(Vector2 hor, Vector2 ver) =>
        Center + new Vector3(hor.X * ver.X, ver.Y, hor.Y * ver.X) * Radius;
}
