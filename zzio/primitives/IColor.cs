using System;
using System.IO;

namespace zzio.primitives {
    [System.Serializable]
    public struct IColor
    {
        public byte r, g, b, a;

        public IColor(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static IColor read(BinaryReader r)
        {
            IColor c;
            c.r = r.ReadByte();
            c.g = r.ReadByte();
            c.b = r.ReadByte();
            c.a = r.ReadByte();
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
