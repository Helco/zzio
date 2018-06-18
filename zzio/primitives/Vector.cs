using System;
using System.IO;

namespace zzio.primitives {
    [System.Serializable]
    public struct Vector {
        public float x, y, z;

        public Vector(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static Vector read(BinaryReader r)
        {
            Vector v;
            v.x = r.ReadSingle();
            v.y = r.ReadSingle();
            v.z = r.ReadSingle();
            return v;
        }

        public void write(BinaryWriter w)
        {
            w.Write(x);
            w.Write(y);
            w.Write(z);
        }
    }
}
