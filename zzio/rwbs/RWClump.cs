using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;

namespace zzio.rwbs
{
    [Serializable]
    public class RWClump : StructSection
    {
        public override SectionId sectionId => SectionId.Clump;

        public UInt32 atomicCount, lightCount, camCount;

        protected override void readStruct(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            atomicCount = lightCount = camCount = 0;
            try
            {
                atomicCount = reader.ReadUInt32();
                lightCount = reader.ReadUInt32();
                camCount = reader.ReadUInt32();
            }
            catch(EndOfStreamException) { } // We don't really care...
        }

        protected override void writeStruct(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(atomicCount);
            writer.Write(lightCount);
            writer.Write(camCount);
        }
    }
}
