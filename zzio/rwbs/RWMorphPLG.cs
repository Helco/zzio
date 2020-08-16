using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;

namespace zzio.rwbs
{
    [Serializable]
    public class RWMorphPLG : Section
    {
        public override SectionId sectionId => SectionId.MorphPLG;

        public UInt32 morphTargetIndex;

        protected override void readBody(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            morphTargetIndex = reader.ReadUInt32();
        }

        protected override void writeBody(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(morphTargetIndex);
        }
    }
}
