using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace zzre;

public readonly partial struct Line : IIntersectable
{
    public readonly Vector3 Start;
    public readonly Vector3 End;
    public Vector3 Vector { [MethodImpl(MathEx.MIOptions)]
        get => End - Start; }
    public Vector3 Direction { [MethodImpl(MathEx.MIOptions)]
        get => Vector3.Normalize(Vector); }
    public float Length { [MethodImpl(MathEx.MIOptions)]
        get => Vector.Length(); }
    public float LengthSq { [MethodImpl(MathEx.MIOptions)]
        get => Vector.LengthSquared(); }

    [MethodImpl(MathEx.MIOptions)]
    public Line(Vector3 start, Vector3 end) => (Start, End) = (start, end);

    [MethodImpl(MathEx.MIOptions)]
    public float PhaseOf(Vector3 point) => Vector3.Dot(point - Start, Vector) / LengthSq;
    [MethodImpl(MathEx.MIOptions)]
    public float UnscaledPhaseOf(Vector3 point) => Vector3.Dot(point - Start, Vector);
    [MethodImpl(MathEx.MIOptions)]
    public Vector3 ClosestPoint(Vector3 point) => Start + Vector * Math.Clamp(PhaseOf(point), 0f, 1f);

    private Raycast? CheckRaycast(Raycast? cast) =>
        cast == null || cast.Value.Distance * cast.Value.Distance <= LengthSq ? cast : null;
    public Raycast? Cast(Sphere sphere) => CheckRaycast(new Ray(Start, Direction).Cast(sphere));
    public Raycast? Cast(Box box) => CheckRaycast(new Ray(Start, Direction).Cast(box));
    public Raycast? Cast(Box box, Location boxLoc) => CheckRaycast(new Ray(Start, Direction).Cast(box.TransformToWorld(boxLoc)));
    public Raycast? Cast(OrientedBox box) => CheckRaycast(new Ray(Start, Direction).Cast(box));
    public Raycast? Cast(Plane plane) => CheckRaycast(new Ray(Start, Direction).Cast(plane));
    public Raycast? Cast(Triangle triangle) => CheckRaycast(new Ray(Start, Direction).Cast(triangle));
}
