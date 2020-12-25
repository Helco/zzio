using System;
using System.Numerics;

namespace zzre
{
    public struct Plane
    {
        private Vector3 normal;
        public Vector3 Normal
        {
            get => normal;
            set
            {
                normal = Math.Abs(value.LengthSquared() - 1.0f) > 0.00001f
                    ? Vector3.Normalize(value)
                    : value;
            }
        }

        public float Distance { get; set; }

        public Plane(Vector3 normal, float distance)
        {
            this.normal = Vector3.Zero;
            Distance = distance;
            Normal = normal;
        }

        public float SignedDistanceTo(Vector3 point) => Vector3.Dot(point, normal) - Distance;
        public float DistanceTo(Vector3 point) => Math.Abs(SignedDistanceTo(point));
        public bool Intersects(Vector3 point) => MathEx.CmpZero(DistanceTo(point));
        public int SideOf(Vector3 point) => Math.Sign(SignedDistanceTo(point));
        public Vector3 ClosestPoint(Vector3 point) => point - normal * SignedDistanceTo(point);

        public bool Intersects(Plane other) => !MathEx.CmpZero(Vector3.Cross(Normal, other.Normal).LengthSquared());
    }
}
