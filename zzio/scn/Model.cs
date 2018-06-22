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
        public string filename;
        public Vector pos, rot, scale;
        public IColor color;
        public byte i1;
        public Int32 i15;
        public byte i2;

        public void read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            idx = reader.ReadUInt32();
            filename = reader.ReadZString();
            pos = Vector.read(reader);
            rot = Vector.read(reader);
            scale = Vector.read(reader);
            color = IColor.read(reader);
            i1 = reader.ReadByte();
            i15 = reader.ReadInt32();
            i2 = reader.ReadByte();
        }

        public void write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(idx);
            writer.WriteZString(filename);
            pos.write(writer);
            rot.write(writer);
            scale.write(writer);
            color.write(writer);
            writer.Write(i1);
            writer.Write(i15);
            writer.Write(i2);
        }
    }
}
