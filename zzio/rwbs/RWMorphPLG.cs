using System;
using System.IO;

namespace zzio.rwbs
{
    [Serializable]
    public class RWMorphPLG : Section
    {
        public override SectionId sectionId => SectionId.MorphPLG;

        public uint morphTargetIndex;

        protected override void readBody(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            morphTargetIndex = reader.ReadUInt32();
        }

        protected override void writeBody(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(morphTargetIndex);
        }
    }
}
