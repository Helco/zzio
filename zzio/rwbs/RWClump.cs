using System;
using System.IO;

namespace zzio.rwbs
{
    [Serializable]
    public class RWClump : StructSection
    {
        public override SectionId sectionId => SectionId.Clump;

        public uint atomicCount, lightCount, camCount;

        protected override void readStruct(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            atomicCount = lightCount = camCount = 0;
            if (stream.Length >= 4)
                atomicCount = reader.ReadUInt32();
            if (stream.Length >= 8)
                lightCount = reader.ReadUInt32();
            if (stream.Length >= 12)
                camCount = reader.ReadUInt32();
        }

        protected override void writeStruct(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(atomicCount);
            writer.Write(lightCount);
            writer.Write(camCount);
        }
    }
}
