﻿using System.Collections.Generic;
using System.Numerics;

namespace zzre
{
    public readonly struct OrientedBox : IRaycastable, IIntersectable
    {
        public readonly Box Box;
        public readonly Quaternion Orientation;

        public OrientedBox(Box box, Quaternion orientation) => (Box, Orientation) = (box, orientation);
        public OrientedBox((Box box, Quaternion orientation) t) => (Box, Orientation) = (t.box, t.orientation);
        public void Deconstruct(out Box box, out Quaternion orientation) => (box, orientation) = (Box, Orientation);

        public static implicit operator OrientedBox(Box box) => new(box, Quaternion.Identity);

        public Raycast? Cast(Ray ray) => ray.Cast(this);
        public Raycast? Cast(Line line) => line.Cast(this);

        public bool Intersects(Box box) => box.Intersects(this);
        public bool Intersects(OrientedBox box) => box.Intersects(this);
        public bool Intersects(Sphere sphere) => sphere.Intersects(this);
        public bool Intersects(Plane plane) => plane.Intersects(this);
        public bool Intersects(Triangle triangle) => triangle.Intersects(this);

        public IReadOnlyList<Vector3> Corners() => Box.Corners(Orientation);
        public IEnumerable<Triangle> Triangles() => Box.Triangles(Orientation);
        public IEnumerable<Line> Edges() => Box.Edges(Orientation);
    }
}
