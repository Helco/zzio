using System;
using System.IO;

namespace zzio.primitives
{
    [System.Serializable]
    public struct TexCoord
    {
        public float u, v;

        public TexCoord(float u, float v)
        {
            this.u = u;
            this.v = v;
        }

        public static TexCoord read(BinaryReader r)
        {
            TexCoord t;
            t.u = r.ReadSingle();
            t.v = r.ReadSingle();
            return t;
        }

        public void write(BinaryWriter w)
        {
            w.Write(u);
            w.Write(v);
        }
    }
}
