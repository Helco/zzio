using System.Collections.Generic;
using System.Numerics;

namespace zzre;

// OrientedBox is a pure mixin structure

public readonly partial struct OrientedBox : IRaycastable, IIntersectable
{
    public readonly Box AABox;
    public readonly Quaternion Orientation;

    public OrientedBox(Box box, Quaternion orientation) => (AABox, Orientation) = (box, orientation);
    public OrientedBox((Box box, Quaternion orientation) t) => (AABox, Orientation) = (t.box, t.orientation);
    public void Deconstruct(out Box box, out Quaternion orientation) => (box, orientation) = (AABox, Orientation);

    public static implicit operator OrientedBox(Box box) => new(box, Quaternion.Identity);

    public IReadOnlyList<Vector3> Corners() => AABox.Corners(Orientation);
    public IEnumerable<Triangle> Triangles() => AABox.Triangles(Orientation);
    public IEnumerable<Line> Edges() => AABox.Edges(Orientation);
}
