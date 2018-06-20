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
        public override SectionId sectionId { get { return SectionId.MorphPLG; } }

        public UInt32 morphTargetIndex;

        protected override void readBody(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            morphTargetIndex = reader.ReadUInt32();
        }

        protected override void writeBody(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(morphTargetIndex);
        }

        public override Section findChildById(SectionId sectionId, bool recursive)
        {
            return null;
        }
    }
}
