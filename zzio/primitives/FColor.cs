using System;
using System.IO;

namespace zzio.primitives {
    [System.Serializable]
    public struct FColor
    {
        public float r, g, b, a;

        public FColor(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static FColor read(BinaryReader r)
        {
            FColor c;
            c.r = r.ReadSingle();
            c.g = r.ReadSingle();
            c.b = r.ReadSingle();
            c.a = r.ReadSingle();
            return c;
        }

        public void write(BinaryWriter w)
        {
            w.Write(r);
            w.Write(g);
            w.Write(b);
            w.Write(a);
        }
    }
}
