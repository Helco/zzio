using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace zzre.rendering
{
    [Flags]
    public enum ViewFrustumIntersection
    {
        Inside = (1 << 0),
        Outside = (1 << 1),
        Intersecting = Inside | Outside
    }

    public class ViewFrustumCulling
    {
        /* Frustum corner order
         * 
         *  0-3 near plane, 4-7 far plane
         *  
         *  2      3
         *  +------+
         *  |      |
         *  +------+
         *  0      1
         */

        private Matrix4x4 inverseViewProjection = Matrix4x4.Identity;
        private Vector3[] frustumCorners = new Vector3[8]; // in world space

        public IReadOnlyList<Vector3> FrustumCorners => frustumCorners;

        public Camera Camera { get; set; }

        public ViewFrustumCulling(Camera camera)
        {
            Camera = camera;
        }

        public void UpateFrustum()
        {
            if (!Matrix4x4.Invert(Camera.View * Camera.Projection, out inverseViewProjection))
                throw new ArgumentException();
            for (int i = 0; i < 8; i++)
            {
                var c = Vector4.Transform(new Vector4(
                    ((i / 1) % 2) * 2 - 1,
                    ((i / 2) % 2) * 2 - 1,
                    ((i / 4) % 2),
                    1),
                    inverseViewProjection);
                frustumCorners[i] = new Vector3(c.X, c.Y, c.Z) / c.W;
            }
        }

        public ViewFrustumIntersection Test(Vector3 normal, float distance)
        {
            int GetSide(Vector3 corner) => Math.Sign(Vector3.Dot(corner, normal) - distance);

            var firstSide = GetSide(FrustumCorners.First());
            foreach (var corner in FrustumCorners.Skip(1))
            {
                var curSide = GetSide(corner);
                if (firstSide != curSide)
                    return ViewFrustumIntersection.Intersecting;
            }
            return firstSide > 0 ? ViewFrustumIntersection.Inside : ViewFrustumIntersection.Outside;
        }

        public ViewFrustumIntersection Test(Box bounds)
        {
            // TODO: Implement AABB ViewFrustumCulling
            return ViewFrustumIntersection.Inside;
        }
    }
}
