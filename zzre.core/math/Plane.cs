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

        public int SideOf(Vector3 point) => Math.Sign(Vector3.Dot(point, normal) - Distance);
    }
}
