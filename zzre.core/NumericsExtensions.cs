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

        // from https://stackoverflow.com/questions/11492299/quaternion-to-euler-angles-algorithm-how-to-convert-to-y-up-and-between-ha
        public static Vector3 ToEuler(this Quaternion q) => new Vector3(
            (float)Math.Asin(2f * (q.X * q.Z - q.W * q.Y)),                                         // Pitch
            (float)Math.Atan2(2f * q.X * q.W + 2f * q.Y * q.Z, 1 - 2f * (q.Z * q.Z + q.W * q.W)),   // Yaw 
            (float)Math.Atan2(2f * q.X * q.Y + 2f * q.Z * q.W, 1 - 2f * (q.Y * q.Y + q.Z * q.Z))    // Roll 
        );
    }
}
