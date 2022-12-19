using System;
using System.IO;

namespace zzio.rwbs
{
    [Serializable]
    public class RWGeometryList : StructSection
    {
        public override SectionId sectionId => SectionId.GeometryList;

        public uint geometryCount;

        protected override void readStruct(Stream stream)
        {
            using BinaryReader reader = new(stream);
            geometryCount = reader.ReadUInt32();
        }

        protected override void writeStruct(Stream stream)
        {
            using BinaryWriter writer = new(stream);
            writer.Write(geometryCount);
        }
    }
}
