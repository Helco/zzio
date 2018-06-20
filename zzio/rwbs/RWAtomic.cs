using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;

namespace zzio.rwbs
{
    [Flags]
    public enum AtomicFlags
    {
        CollisionTest = 0x01,
        Render = 0x02
    }

    [Serializable]
    public class RWAtomic : StructSection
    {
        public override SectionId sectionId { get { return SectionId.Atomic; } }

        public UInt32 frameIndex, geometryIndex;
        public AtomicFlags flags;

        protected override void readStruct(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            frameIndex = reader.ReadUInt32();
            geometryIndex = reader.ReadUInt32();
            flags = EnumUtils.intToFlags<AtomicFlags>(reader.ReadUInt32());
        }

        protected override void writeStruct(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(frameIndex);
            writer.Write(geometryIndex);
            writer.Write((UInt32)flags);
            writer.Write((UInt32)0); // unused value
        }
    }
}
