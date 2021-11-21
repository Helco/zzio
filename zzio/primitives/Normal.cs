using System.IO;

namespace zzio
{
    [System.Serializable]
    public struct Normal
    {
        public byte x, y, z;
        public sbyte p;

        public Normal(byte x, byte y, byte z, sbyte p)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.p = p;
        }

        public static Normal ReadNew(BinaryReader r)
        {
            Normal n;
            n.x = r.ReadByte();
            n.y = r.ReadByte();
            n.z = r.ReadByte();
            n.p = r.ReadSByte();
            return n;
        }

        public void Write(BinaryWriter w)
        {
            w.Write(x);
            w.Write(y);
            w.Write(z);
            w.Write(p);
        }
    }
}
