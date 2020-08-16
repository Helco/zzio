using System;
using System.Text;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
    [Serializable]
    public class Model : ISceneSection
    {
        public UInt32 idx;
        public string filename = "";
        public Vector pos, rot, scale;
        public IColor color;
        public byte i1;
        public Int32 i15;
        public byte i2;

        public void Read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            idx = reader.ReadUInt32();
            filename = reader.ReadZString();
            pos = Vector.ReadNew(reader);
            rot = Vector.ReadNew(reader);
            scale = Vector.ReadNew(reader);
            color = IColor.ReadNew(reader);
            i1 = reader.ReadByte();
            i15 = reader.ReadInt32();
            i2 = reader.ReadByte();
        }

        public void Write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(idx);
            writer.WriteZString(filename);
            pos.Write(writer);
            rot.Write(writer);
            scale.Write(writer);
            color.Write(writer);
            writer.Write(i1);
            writer.Write(i15);
            writer.Write(i2);
        }
    }
}
