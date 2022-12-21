using System;
using System.IO;

namespace zzio.rwbs;

[Serializable]
public class RWMaterialList : StructSection
{
    public override SectionId sectionId => SectionId.MaterialList;

    public int[] materialIndices = Array.Empty<int>();

    protected override void readStruct(Stream stream)
    {
        using BinaryReader reader = new(stream);
        uint count = reader.ReadUInt32();
        materialIndices = new int[count];
        for (uint i = 0; i < count; i++)
            materialIndices[i] = reader.ReadInt32();
    }

    protected override void writeStruct(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write((uint)materialIndices.Length);
        foreach (int index in materialIndices)
            writer.Write(index);
    }
}
