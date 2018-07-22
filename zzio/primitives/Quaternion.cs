using System;
using System.IO;

namespace zzio.primitives
{
    [System.Serializable]
    public struct Quaternion
    {
        public float x, y, z, w;

        public Quaternion(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public static Quaternion ReadNew(BinaryReader r)
        {
            Quaternion q;
            q.x = r.ReadSingle();
            q.y = r.ReadSingle();
            q.z = r.ReadSingle();
            q.w = r.ReadSingle();
            return q;
        }

        public void Write(BinaryWriter w)
        {
            w.Write(x);
            w.Write(y);
            w.Write(z);
            w.Write(this.w);
        }
    }
}
