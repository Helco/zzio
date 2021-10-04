using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace zzre
{
    [Flags]
    public enum PlaneIntersections
    {
        Inside = (1 << 0),
        Outside = (1 << 1),
        Intersecting = Inside | Outside
    }

    public struct Frustum
    {
        /* Frustum corner order:
         * 0-3 near plane, 4-7 far plane
         *  
         *  2      3
         *  +------+
         *  |      |
         *  +------+
         *  0      1
         *  
         *  Plane order:
         *  Left, Right,
         *  Bottom, Top,
         *  Near, Far
         */

        private Vector3[] corners;
        private Plane[] planes;
        private Matrix4x4 projection, invProjection;

        public IReadOnlyList<Vector3> Corners => corners;
        public IReadOnlyList<Plane> Planes => planes;

        public Matrix4x4 InverseProjection
        {
            get => invProjection;
            set
            {
                if (!Matrix4x4.Invert(value, out projection))
                    throw new ArgumentException("Inverse projection matrix could not be inverted");
                invProjection = value;
                UpdateGeometry();
            }
        }

        public Matrix4x4 Projection
        {
            get => projection;
            set
            {
                if (!Matrix4x4.Invert(value, out invProjection))
                    throw new ArgumentException("Projection matrix could not be inverted");
                projection = value;
                UpdateGeometry();
            }
        }

        private void UpdateGeometry()
        {
            if (corners == null)
                corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var c = Vector4.Transform(new Vector4(
                    ((i / 1) % 2) * 2 - 1,
                    ((i / 2) % 2) * 2 - 1,
                    ((i / 4) % 2),
                    1),
                    invProjection);
                corners[i] = new Vector3(c.X, c.Y, c.Z) / c.W;
            }

            // according to http://www8.cs.umu.se/kurser/5DV051/HT12/lab/plane_extraction.pdf
            if (planes == null)
                planes = new Plane[6];
            var row4 = new Vector3(Projection.M14, Projection.M24, Projection.M34);
            planes[0].Normal = row4 + new Vector3(Projection.M11, Projection.M21, Projection.M31);
            planes[0].Distance = Projection.M44 + Projection.M41;
            planes[1].Normal = row4 - new Vector3(Projection.M11, Projection.M21, Projection.M31);
            planes[1].Distance = Projection.M44 - Projection.M41;
            planes[2].Normal = row4 + new Vector3(Projection.M12, Projection.M22, Projection.M32);
            planes[2].Distance = Projection.M44 + Projection.M42;
            planes[3].Normal = row4 - new Vector3(Projection.M12, Projection.M22, Projection.M32);
            planes[3].Distance = Projection.M44 - Projection.M42;
            planes[4].Normal = new Vector3(Projection.M13, Projection.M23, Projection.M33);
            planes[4].Distance = Projection.M43; // note no substraction because our depth range is 0->1
            planes[5].Normal = row4 - new Vector3(Projection.M13, Projection.M23, Projection.M33);
            planes[5].Distance = Projection.M44 - Projection.M43;
        }

        public PlaneIntersections Intersects(Plane plane)
        {
            var firstSide = plane.SideOf(corners.First());
            var isIntersecting = corners
                .Skip(1)
                .Select(corner => plane.SideOf(corner))
                .Any(cornerSide => cornerSide != firstSide);
            return
                isIntersecting ? PlaneIntersections.Intersecting
                : firstSide > 0 ? PlaneIntersections.Inside
                : PlaneIntersections.Outside;
        }
    }
}
