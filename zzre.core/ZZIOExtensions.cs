using System;
using System.Numerics;
namespace zzre.core
{
    public static class ZZIOExtensions
    {
        public static Vector3 ToNumerics(this zzio.primitives.Vector v) => new Vector3(v.x, v.y, v.z);
        public static Quaternion ToNumerics(this zzio.primitives.Quaternion q) => new Quaternion(q.x, q.y, q.z, q.w);
        public static Vector2 ToNumerics(this zzio.primitives.TexCoord t) => new Vector2(t.u, t.v);
    }
}
