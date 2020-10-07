using System;
using System.Numerics;

namespace zzre
{
    public static class NumericsExtensions
    {
        private const float EPS = 0.001f;

        public static Vector3 SomeOrthogonal(this Vector3 v) => Math.Abs(v.Y) < EPS && Math.Abs(v.Z) < EPS
            ? Vector3.Cross(v, Vector3.UnitY)
            : Vector3.Cross(v, Vector3.UnitX);

        // from https://en.wikipedia.org/wiki/Conversion_between_quaternions_and_Euler_angles
        public static Vector3 ToEuler(this Quaternion q)
        {
            var euler = Vector3.Zero;
            double sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            double cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            euler.Z = (float)Math.Atan2(sinr_cosp, cosr_cosp);

            double sinp = 2 * (q.W * q.Y - q.Z * q.X);
            euler.X = Math.Abs(sinp) >= 1
                ? (float)Math.CopySign(Math.PI / 2, sinp) // use 90 degrees if out of range
                : (float)Math.Asin(sinp);

            double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            euler.Y = (float)Math.Atan2(siny_cosp, cosy_cosp);

            return euler;
        }

        public static (Vector3 right, Vector3 up, Vector3 forward) UnitVectors(this Quaternion q) => (
            Vector3.Transform(Vector3.UnitX, q),
            Vector3.Transform(Vector3.UnitY, q),
            Vector3.Transform(Vector3.UnitZ, q));
    }
}
