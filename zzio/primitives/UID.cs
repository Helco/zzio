using System;
using System.IO;
using System.Text;

namespace zzio.primitives
{
    [Serializable]
    public struct UID
    {
        public readonly UInt32 raw;
        public int Module => (int)(raw % 16);

        public UID(UInt32 raw = 0)
        {
            this.raw = raw;
        }

        public override int GetHashCode()
        {
            return raw.GetHashCode();
        }

        public static UID ReadNew(BinaryReader reader)
        {
            return new UID(reader.ReadUInt32());
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(raw);
        }

        public static UID Parse(string text)
        {
            return new UID(Convert.ToUInt32(text, 16));
        }

        public override string ToString()
        {
            return raw.ToString("X").PadLeft(8, '0');
        }
    }
}
