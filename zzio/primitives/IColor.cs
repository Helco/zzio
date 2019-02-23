using System;
using System.IO;

namespace zzio.primitives
{
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

        public IColor(uint c)
        {
            this.r = (byte)((c >> 0) & 0xff);
            this.g = (byte)((c >> 8) & 0xff);
            this.b = (byte)((c >> 16) & 0xff);
            this.a = (byte)((c >> 24) & 0xff);
        }

        public static IColor ReadNew(BinaryReader r)
        {
            IColor c;
            c.r = r.ReadByte();
            c.g = r.ReadByte();
            c.b = r.ReadByte();
            c.a = r.ReadByte();
            return c;
        }

        public void Write(BinaryWriter w)
        {
            w.Write(r);
            w.Write(g);
            w.Write(b);
            w.Write(a);
        }
    }
}
