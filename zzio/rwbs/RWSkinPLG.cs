using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace zzio.rwbs;

[Flags]
public enum BoneFlags
{
    // the naming might be weird as it was intended
    // by RenderWare to represent matrix operations
    IsChildless = (1 << 0),
    HasNextSibling = (1 << 1),

    UnknownFlag3 = (1 << 3)
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Bone
{
    public uint id, idx;
    public BoneFlags flags;
    public Matrix4x4 objectToBone;

    public const int ExpectedSize = (3 + 4 * 4) * 4;
}

[Serializable]
public class RWSkinPLG : Section
{
    public const int BonesPerVertex = 4;
    public override SectionId sectionId => SectionId.SkinPLG;

    public byte[] vertexIndices = [];
    public float[] vertexWeights = []; // 4 per vertex
    public Bone[] bones = [];

    protected override void readBody(Stream stream)
    {
        using BinaryReader reader = new(stream);
        bones = new Bone[reader.ReadUInt32()];
        int vertexCount = reader.ReadInt32();

        vertexIndices = reader.ReadStructureArray<byte>(vertexCount * BonesPerVertex, expectedSizeOfElement: 1);
        vertexWeights = reader.ReadStructureArray<float>(vertexCount * BonesPerVertex, expectedSizeOfElement: 4);
        reader.ReadStructureArray(bones, Bone.ExpectedSize);
    }

    protected override void writeBody(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(bones.Length);
        int vertexCount = vertexIndices.GetLength(0);
        writer.Write(vertexCount);

        writer.WriteStructureArray(vertexIndices, expectedSizeOfElement: 1);
        writer.WriteStructureArray(vertexWeights, expectedSizeOfElement: 4);
        writer.WriteStructureArray(bones, expectedSizeOfElement: Bone.ExpectedSize);
    }
}