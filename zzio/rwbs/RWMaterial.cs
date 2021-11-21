using System;
using System.IO;

namespace zzio.rwbs
{
    [Serializable]
    public class RWMaterial : StructSection
    {
        public override SectionId sectionId => SectionId.Material;

        public UInt32 flags; // unkown flags enum
        public IColor color;
        public bool isTextured;
        public float ambient, specular, diffuse;

        protected override void readStruct(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            flags = reader.ReadUInt32();
            color = IColor.ReadNew(reader);
            reader.ReadUInt32(); // unused
            isTextured = reader.ReadUInt32() > 0;
            if (stream.Length - stream.Position > 0)
                ambient = reader.ReadSingle();
            if (stream.Length - stream.Position > 0)
                specular = reader.ReadSingle();
            if (stream.Length - stream.Position > 0)
                diffuse = reader.ReadSingle();
        }

        protected override void writeStruct(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(flags);
            writer.Write((UInt32)0);
            color.Write(writer);
            writer.Write((UInt32)(isTextured ? 1 : 0));
            writer.Write(ambient);
            writer.Write(specular);
            writer.Write(diffuse);
        }
    }
}
