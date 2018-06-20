using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;
using zzio.primitives;

namespace zzio.rwbs
{
    [Serializable]
    public class RWPlaneSection : StructSection
    {
        public override SectionId sectionId { get { return SectionId.PlaneSection; } }

        public UInt32 sectorType; // unknown enum
        public float value, leftValue, rightValue;
        public bool leftIsWorldSector, rightIsWorldSector;

        protected override void readStruct(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            sectorType = reader.ReadUInt32();
            value = reader.ReadSingle();
            leftIsWorldSector = reader.ReadUInt32() > 0;
            rightIsWorldSector = reader.ReadUInt32() > 0;
            leftValue = reader.ReadSingle();
            rightValue = reader.ReadSingle();
        }

        protected override void writeStruct(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(sectorType);
            writer.Write(value);
            writer.Write((UInt32)(leftIsWorldSector ? 1 : 0));
            writer.Write((UInt32)(rightIsWorldSector ? 1 : 0));
            writer.Write(leftValue);
            writer.Write(rightValue);
        }
    }
}
