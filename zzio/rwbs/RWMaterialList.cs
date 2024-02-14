using System;
using System.IO;

namespace zzio.rwbs;

[Serializable]
public class RWMaterialList : StructSection
{
    public override SectionId sectionId => SectionId.MaterialList;

    public int[] materialIndices = [];

    protected override void readStruct(Stream stream)
    {
        using BinaryReader reader = new(stream);
        uint count = reader.ReadUInt32();
        materialIndices = reader.ReadStructureArray<int>((int)count, expectedSizeOfElement: 4);
    }

    protected override void writeStruct(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write((uint)materialIndices.Length);
        writer.WriteStructureArray(materialIndices, expectedSizeOfElement: 4);
    }
}
