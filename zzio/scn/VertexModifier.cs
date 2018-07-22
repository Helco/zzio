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

        public void Read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            idx = reader.ReadUInt32();
            type = reader.ReadUInt32();
            v = Vector.ReadNew(reader);
            color = IColor.ReadNew(reader);
            if (type == 1)
                vv = Vector.ReadNew(reader);
            else
                vv = new Vector();
            ii = reader.ReadUInt32();
            c = reader.ReadByte();
        }

        public void Write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(idx);
            writer.Write(type);
            v.Write(writer);
            color.Write(writer);
            if (type == 1)
                vv.Write(writer);
            writer.Write(ii);
            writer.Write(c);
        }
    }
}
