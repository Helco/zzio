using System;
using System.IO;

namespace zzio.rwbs
{
    [Serializable]
    public class RWMaterialList : StructSection
    {
        public override SectionId sectionId => SectionId.MaterialList;

        public Int32[] materialIndices = Array.Empty<Int32>();

        protected override void readStruct(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            UInt32 count = reader.ReadUInt32();
            materialIndices = new Int32[count];
            for (UInt32 i = 0; i < count; i++)
                materialIndices[i] = reader.ReadInt32();
        }

        protected override void writeStruct(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write((UInt32)materialIndices.Length);
            foreach (Int32 index in materialIndices)
                writer.Write(index);
        }
    }
}
