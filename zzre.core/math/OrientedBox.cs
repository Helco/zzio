using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace zzre;

// OrientedBox is a pure mixin structure

public readonly partial struct OrientedBox : IRaycastable, IIntersectable
{
    public readonly Box AABox;
    public readonly Quaternion Orientation;

    [MethodImpl(MathEx.MIOptions)]
    public OrientedBox(Box box, Quaternion orientation) => (AABox, Orientation) = (box, orientation);
    [MethodImpl(MathEx.MIOptions)]
    public OrientedBox((Box box, Quaternion orientation) t) => (AABox, Orientation) = (t.box, t.orientation);
    [MethodImpl(MathEx.MIOptions)]
    public void Deconstruct(out Box box, out Quaternion orientation) => (box, orientation) = (AABox, Orientation);

    [MethodImpl(MathEx.MIOptions)]
    public static implicit operator OrientedBox(Box box) => new(box, Quaternion.Identity);

    public IReadOnlyList<Vector3> Corners() => AABox.Corners(Orientation);
    public IEnumerable<Triangle> Triangles() => AABox.Triangles(Orientation);
    public IEnumerable<Line> Edges() => AABox.Edges(Orientation);
}
