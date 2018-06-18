using System;
using System.IO;

namespace zzio.primitives
{
    [System.Serializable]
    public struct Triangle
    {
        public UInt16 m, v1, v2, v3;

        public Triangle(UInt16 v1, UInt16 v2, UInt16 v3, UInt16 m)
        {
            this.m = m;
            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;
        }

        public static Triangle read(BinaryReader r)
        {
            Triangle t;
            t.m = r.ReadUInt16();
            t.v1 = r.ReadUInt16();
            t.v2 = r.ReadUInt16();
            t.v3 = r.ReadUInt16();
            return t;
        }

        public void write(BinaryWriter w)
        {
            w.Write(m);
            w.Write(v1);
            w.Write(v2);
            w.Write(v3);
        }
    }
}
