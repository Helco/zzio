using System;
using System.IO;

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

        public uint frameIndex, geometryIndex;
        public AtomicFlags flags;

        protected override void readStruct(Stream stream)
        {
            using BinaryReader reader = new(stream);
            frameIndex = reader.ReadUInt32();
            geometryIndex = reader.ReadUInt32();
            flags = EnumUtils.intToFlags<AtomicFlags>(reader.ReadUInt32());
        }

        protected override void writeStruct(Stream stream)
        {
            using BinaryWriter writer = new(stream);
            writer.Write(frameIndex);
            writer.Write(geometryIndex);
            writer.Write((uint)flags);
            writer.Write((uint)0); // unused value
        }
    }
}
