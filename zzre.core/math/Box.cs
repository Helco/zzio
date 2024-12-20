﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace zzre;

// unfortunately very much copy-paste from Rect, CSharp has no better generics support

public readonly partial struct Box : IRaycastable, IIntersectable
{
    public readonly Vector3 Center;
    public readonly Vector3 Size;

    public Vector3 HalfSize { [MethodImpl(MathEx.MIOptions)]
        get => Size / 2; }
    public Vector3 Min { [MethodImpl(MathEx.MIOptions)]
        get => Center - HalfSize; }
    public Vector3 Max { [MethodImpl(MathEx.MIOptions)]
        get => Center + HalfSize; }
    public float MaxSizeComponent { [MethodImpl(MathEx.MIOptions)]
        get => Math.Max(Math.Max(Size.X, Size.Y), Size.Z); }
    public float HalfArea { [MethodImpl(MathEx.MIOptions)]
        get => Size.X * Size.Y + Size.X * Size.Z + Size.Y * Size.Z;
    }
    public float Area { [MethodImpl(MathEx.MIOptions)]
        get => HalfArea * 2;
    }
    public float Volume { [MethodImpl(MathEx.MIOptions)]
        get => Size.X * Size.Y * Size.Z;
    }

    public static readonly Box Zero;

    [MethodImpl(MathEx.MIOptions)]
    public Box(float x, float y, float z, float w, float h, float d)
    {
        Center = new Vector3(x, y, z);
        Size = new Vector3(w, h, d);
    }

    [MethodImpl(MathEx.MIOptions)]
    public Box(Vector3 center, Vector3 size)
    {
        Center = center;
        Size = size;
    }

    [MethodImpl(MathEx.MIOptions)]
    public static Box FromMinMax(Vector3 min, Vector3 max) => new((min + max) / 2, (max - min));
    [MethodImpl(MathEx.MIOptions)]
    public static Box Union(params Box[] bounds) => bounds.Skip(1).Aggregate(bounds.First(), (prev, next) => prev.Union(next));

    [MethodImpl(MathEx.MIOptions)]
    public Box OffsettedBy(float x, float y, float z) => new(Center + new Vector3(x, y, z), Size);
    [MethodImpl(MathEx.MIOptions)]
    public Box OffsettedBy(Vector3 off) => new(Center + off, Size);
    [MethodImpl(MathEx.MIOptions)]
    public Box GrownBy(float x, float y, float z) => new(Center, Size + new Vector3(x, y, z));
    [MethodImpl(MathEx.MIOptions)]
    public Box GrownBy(Vector3 off) => new(Center, Size + off);
    [MethodImpl(MathEx.MIOptions)]
    public Box ScaledBy(float s) => new(Center, Size * s);
    [MethodImpl(MathEx.MIOptions)]
    public Box ScaledBy(float x, float y, float z) => new(Center, Vector3.Multiply(Size, new Vector3(x, y, z)));
    [MethodImpl(MathEx.MIOptions)]
    public Box ScaledBy(Vector3 s) => new(Center, Vector3.Multiply(Size, s));
    [MethodImpl(MathEx.MIOptions)]
    public Box At(Vector3 newCenter) => new(newCenter, Size);
    [MethodImpl(MathEx.MIOptions)]
    public Box WithSizeOf(Vector3 newSize) => new(Center, newSize);
    [MethodImpl(MathEx.MIOptions)]
    public Box Union(Box other) => FromMinMax(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));
    [MethodImpl(MathEx.MIOptions)]
    public Box Union(Vector3 v) => Union(new Box(v, Vector3.Zero));

    // TODO: Replace allocations in Box
    public IReadOnlyList<Vector3> Corners() => new[]
    {
        new Vector3(Min.X, Min.Y, Min.Z),
        new Vector3(Max.X, Min.Y, Min.Z),
        new Vector3(Min.X, Max.Y, Min.Z),
        new Vector3(Max.X, Max.Y, Min.Z),
        new Vector3(Min.X, Min.Y, Max.Z),
        new Vector3(Max.X, Min.Y, Max.Z),
        new Vector3(Min.X, Max.Y, Max.Z),
        new Vector3(Max.X, Max.Y, Max.Z)
    };

    public IReadOnlyList<Vector3> Corners(Quaternion q)
    {
        var (right, up, forward) = q.UnitVectors();
        right *= HalfSize.X;
        up *= HalfSize.Y;
        forward *= HalfSize.Z;
        return new[]
        {
            Center - right - up - forward,
            Center + right - up - forward,
            Center - right + up - forward,
            Center + right + up - forward,
            Center - right - up + forward,
            Center + right - up + forward,
            Center - right + up + forward,
            Center + right + up + forward,
        };
    }

    public IReadOnlyList<Vector3> Corners(Location loc)
    {
        var (box, q) = TransformToWorld(loc);
        return box.Corners(q);
    }

    public IReadOnlyList<Plane> Planes() => new[]
    {
        new Plane(-Vector3.UnitX, Min.X),
        new Plane(Vector3.UnitX, Max.X),
        new Plane(-Vector3.UnitY, Min.Z),
        new Plane(Vector3.UnitY, Max.Z),
        new Plane(-Vector3.UnitZ, Min.Z),
        new Plane(Vector3.UnitZ, Max.Z),
    };

    [MethodImpl(MathEx.MIOptions)]
    public OrientedBox TransformToWorld(Location location)
    {
        var newCenter = Vector3.Transform(Center, location.LocalToWorld);
        return new OrientedBox(new Box(newCenter, Size), location.GlobalRotation);
    }

    [MethodImpl(MathEx.MIOptions)]
    public Vector3 ClosestPoint(Vector3 to)
    {
        var min = Min;
        var max = Max;
        return new Vector3(
            Math.Clamp(to.X, min.X, max.X),
            Math.Clamp(to.Y, min.Y, max.Y),
            Math.Clamp(to.Z, min.Z, max.Z));
    }

    [MethodImpl(MathEx.MIOptions)]
    public Vector3 ClosestPoint(Location location, Vector3 to)
    {
        var (transformed, orientation) = TransformToWorld(location);
        return transformed.ClosestPoint(orientation, to);
    }

    [MethodImpl(MathEx.MIOptions)]
    public Vector3 ClosestPoint(Quaternion orientation, Vector3 to)
    {
        var dir = to - Center;
        var (right, up, forward) = orientation.UnitVectors();
        var distanceX = Vector3.Dot(right, dir);
        var distanceY = Vector3.Dot(up, dir);
        var distanceZ = Vector3.Dot(forward, dir);
        return Center +
            right * Math.Clamp(distanceX, -HalfSize.X, +HalfSize.X) +
            up * Math.Clamp(distanceY, -HalfSize.Y, +HalfSize.Y) +
            forward * Math.Clamp(distanceZ, -HalfSize.Z, +HalfSize.Z);
    }

    [MethodImpl(MathEx.MIOptions)]
    internal Interval IntervalOn(Vector3 axis) => new(Corners().Select(c => Vector3.Dot(c, axis)));

    [MethodImpl(MathEx.MIOptions)]
    internal Interval IntervalOn(Quaternion orientation, Vector3 axis) => new(Corners(orientation).Select(c => Vector3.Dot(c, axis)));

    public IEnumerable<Triangle> Triangles() => Triangles(Quaternion.Identity);
    public IEnumerable<Triangle> Triangles(Quaternion q)
    {
        var corners = Corners(q);
        yield return new Triangle(corners[0], corners[1], corners[2]);
        yield return new Triangle(corners[2], corners[1], corners[3]);
        yield return new Triangle(corners[5], corners[4], corners[7]);
        yield return new Triangle(corners[7], corners[4], corners[6]);
        yield return new Triangle(corners[4], corners[0], corners[6]);
        yield return new Triangle(corners[6], corners[0], corners[2]);
        yield return new Triangle(corners[1], corners[5], corners[3]);
        yield return new Triangle(corners[3], corners[5], corners[7]);
        yield return new Triangle(corners[2], corners[3], corners[6]);
        yield return new Triangle(corners[6], corners[3], corners[7]);
        yield return new Triangle(corners[4], corners[5], corners[0]);
        yield return new Triangle(corners[0], corners[5], corners[1]);
    }

    public IEnumerable<Line> Edges() => Edges(Quaternion.Identity);
    public IEnumerable<Line> Edges(Quaternion q)
    {
        var corners = Corners(q);
        yield return new Line(corners[0], corners[1]);
        yield return new Line(corners[0], corners[2]);
        yield return new Line(corners[3], corners[1]);
        yield return new Line(corners[3], corners[2]);

        yield return new Line(corners[4], corners[5]);
        yield return new Line(corners[4], corners[6]);
        yield return new Line(corners[7], corners[5]);
        yield return new Line(corners[7], corners[6]);

        yield return new Line(corners[0], corners[4]);
        yield return new Line(corners[1], corners[5]);
        yield return new Line(corners[2], corners[6]);
        yield return new Line(corners[3], corners[7]);
    }
}
