using System;
using System.IO;

namespace zzio.rwbs;

public enum BinMeshType
{
    TriList = 0,
    TriStrip = 1,

    Unknown = -1
}

[System.Serializable]
public struct SubMesh
{
    public uint matIndex;
    public uint[] indices; // as ZanZarah uses DirectX, this should always be 32 bit 
}

[Serializable]
public class RWBinMeshPLG : Section
{
    public override SectionId sectionId => SectionId.BinMeshPLG;

    public BinMeshType type;
    public uint totalIndexCount;
    public SubMesh[] subMeshes = Array.Empty<SubMesh>();

    protected override void readBody(Stream stream)
    {
        using BinaryReader reader = new(stream);
        type = EnumUtils.intToEnum<BinMeshType>(reader.ReadInt32());
        subMeshes = new SubMesh[reader.ReadUInt32()];
        totalIndexCount = reader.ReadUInt32();

        foreach (ref SubMesh m in subMeshes.AsSpan())
        {
            m.indices = new uint[reader.ReadUInt32()];
            m.matIndex = reader.ReadUInt32();
            reader.ReadStructureArray(m.indices, expectedSizeOfElement: 4);
        }
    }

    protected override void writeBody(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write((int)type);
        writer.Write(subMeshes.Length);
        writer.Write(totalIndexCount);

        foreach (ref readonly SubMesh m in subMeshes.AsSpan())
        {
            writer.Write(m.indices.Length);
            writer.Write(m.matIndex);
            writer.WriteStructureArray(m.indices, expectedSizeOfElement: 4);
        }
    }
}
