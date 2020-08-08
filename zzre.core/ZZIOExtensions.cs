using System;
using System.Numerics;

namespace zzre
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

        public static Matrix4x4 ToNumerics(this zzio.primitives.Matrix m) => new Matrix4x4(
            m.right.x, m.right.y, m.right.z, m.right.w,
            m.up.x, m.up.y, m.up.z, m.up.w,
            m.forward.x, m.forward.y, m.forward.z, m.forward.w,
            m.pos.x, m.pos.y, m.pos.z, m.pos.w);

        // because I am still not all that sure...
        public static Matrix4x4 ToNumericsTransposed(this zzio.primitives.Matrix m) => new Matrix4x4(
            m.right.x, m.up.x, m.forward.x, m.pos.x,
            m.right.y, m.up.y, m.forward.y, m.pos.y,
            m.right.z, m.up.z, m.forward.z, m.pos.z,
            m.right.w, m.up.w, m.forward.w, m.pos.w);
    }
}
