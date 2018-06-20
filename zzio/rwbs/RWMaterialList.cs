using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;

namespace zzio.rwbs
{
    [Serializable]
    public class RWMaterialList : StructSection
    {
        public override SectionId sectionId { get { return SectionId.MaterialList; } }

        public Int32[] materialIndices = new Int32[0];

        protected override void readStruct(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            UInt32 count = reader.ReadUInt32();
            materialIndices = new Int32[count];
            for (UInt32 i = 0; i < count; i++)
                materialIndices[i] = reader.ReadInt32();
        }

        protected override void writeStruct(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write((UInt32)materialIndices.Length);
            foreach (Int32 index in materialIndices)
                writer.Write(index);
        }
    }
}
