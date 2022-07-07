using System;
using System.IO;
using System.Numerics;

namespace zzio.scn
{
    [Serializable]
    public class VertexModifier : ISceneSection
    {
        public uint idx, type;
        public Vector3 v;
        public IColor color;
        public Vector3 vv;
        public uint ii;
        public byte c;

        public void Read(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            idx = reader.ReadUInt32();
            type = reader.ReadUInt32();
            v = reader.ReadVector3();
            color = IColor.ReadNew(reader);
            vv = type == 1
                ? reader.ReadVector3()
                : new Vector3();
            ii = reader.ReadUInt32();
            c = reader.ReadByte();
        }

        public void Write(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(idx);
            writer.Write(type);
            writer.Write(v);
            color.Write(writer);
            if (type == 1)
                writer.Write(vv);
            writer.Write(ii);
            writer.Write(c);
        }
    }
}
