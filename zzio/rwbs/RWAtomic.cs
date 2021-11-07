using System;
using System.IO;
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
        public override SectionId sectionId => SectionId.Atomic;

        public UInt32 frameIndex, geometryIndex;
        public AtomicFlags flags;

        protected override void readStruct(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            frameIndex = reader.ReadUInt32();
            geometryIndex = reader.ReadUInt32();
            flags = EnumUtils.intToFlags<AtomicFlags>(reader.ReadUInt32());
        }

        protected override void writeStruct(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(frameIndex);
            writer.Write(geometryIndex);
            writer.Write((UInt32)flags);
            writer.Write((UInt32)0); // unused value
        }
    }
}
