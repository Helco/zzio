using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;

namespace zzio.rwbs
{
    [Serializable]
    public class RWGeometryList : StructSection
    {
        public override SectionId sectionId => SectionId.GeometryList;

        public UInt32 geometryCount;

        protected override void readStruct(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            geometryCount = reader.ReadUInt32();
        }

        protected override void writeStruct(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(geometryCount);
        }
    }
}
