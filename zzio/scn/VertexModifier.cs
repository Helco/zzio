using System;
using System.Text;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
    [Serializable]
    public class VertexModifier : ISceneSection
    {
        public UInt32 idx, type;
        public Vector v;
        public IColor color;
        public Vector vv;
        public UInt32 ii;
        public byte c;

        public void read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            idx = reader.ReadUInt32();
            type = reader.ReadUInt32();
            v = Vector.read(reader);
            color = IColor.read(reader);
            if (type == 1)
                vv = Vector.read(reader);
            else
                vv = new Vector();
            ii = reader.ReadUInt32();
            c = reader.ReadByte();
        }

        public void write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(idx);
            writer.Write(type);
            v.write(writer);
            color.write(writer);
            if (type == 1)
                vv.write(writer);
            writer.Write(ii);
            writer.Write(c);
        }
    }
}
