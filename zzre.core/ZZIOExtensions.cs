using System;
using System.Numerics;
namespace zzre.core
{
    public static class ZZIOExtensions
    {
        public static Vector3 ToNumerics(this zzio.primitives.Vector v) => new Vector3(v.x, v.y, v.z);
        public static Quaternion ToNumerics(this zzio.primitives.Quaternion q) => new Quaternion(q.x, q.y, q.z, q.w);
        public static Vector2 ToNumerics(this zzio.primitives.TexCoord t) => new Vector2(t.u, t.v);
        public static Vector4 ToNumerics(this zzio.primitives.FColor c) => new Vector4(c.r, c.g, c.b, c.a);
        public static void CopyFromNumerics(this ref zzio.primitives.FColor c, Vector4 v)
        {
            c.r = v.X;
            c.g = v.Y;
            c.b = v.Z;
            c.a = v.W;
        }
    }
}
