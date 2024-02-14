using System;
using System.IO;
using System.Numerics;

namespace zzio.rwbs;

[Serializable]
public class RWAtomicSection : StructSection
{
    public override SectionId sectionId => SectionId.AtomicSection;

    public uint matIdBase;
    public Vector3 bbox1, bbox2;
    public Vector3[] vertices = [];
    public Normal[] normals = [];
    public IColor[] colors = [];
    public Vector2[]
        texCoords1 = [],
        texCoords2 = [];
    public VertexTriangle[] triangles = [];

    protected override void readStruct(Stream stream)
    {
        if (FindParentById(SectionId.World) is not RWWorld world)
            throw new InvalidDataException("RWAtomicSection has to be child of RWWorld");
        GeometryFormat worldFormat = world.format;

        using BinaryReader reader = new(stream);
        matIdBase = reader.ReadUInt32();
        triangles = new VertexTriangle[reader.ReadUInt32()];
        vertices = new Vector3[reader.ReadUInt32()];
        bbox1 = reader.ReadVector3();
        bbox2 = reader.ReadVector3();
        reader.ReadUInt32(); // unused
        reader.ReadUInt32();

        reader.ReadStructureArray(vertices, expectedSizeOfElement: 12);

        if ((worldFormat & GeometryFormat.Normals) > 0)
            normals = reader.ReadStructureArray<Normal>(vertices.Length, expectedSizeOfElement: 4);

        if ((worldFormat & GeometryFormat.Prelit) > 0)
            colors = reader.ReadStructureArray<IColor>(vertices.Length, expectedSizeOfElement: 4);

        if ((worldFormat & (GeometryFormat.Textured | GeometryFormat.Textured2)) > 0)
            texCoords1 = reader.ReadStructureArray<Vector2>(vertices.Length, expectedSizeOfElement: 8);

        if ((worldFormat & GeometryFormat.Textured2) > 0)
            texCoords2 = reader.ReadStructureArray<Vector2>(vertices.Length, expectedSizeOfElement: 8);

        reader.ReadStructureArray(triangles, expectedSizeOfElement: 8);
    }

    protected override void writeStruct(Stream stream)
    {
        if (FindParentById(SectionId.World) is not RWWorld world)
            throw new InvalidDataException("RWAtomicSection has to be child of RWWorld");
        GeometryFormat worldFormat = world.format;

        using BinaryWriter writer = new(stream);
        writer.Write(matIdBase);
        writer.Write(triangles.Length);
        writer.Write(vertices.Length);
        writer.Write(bbox1);
        writer.Write(bbox2);
        writer.Write(0U);
        writer.Write(0U);

        writer.WriteStructureArray(vertices, expectedSizeOfElement: 12);

        if ((worldFormat & GeometryFormat.Normals) > 0)
            writer.WriteStructureArray(normals, expectedSizeOfElement: 4);

        if ((worldFormat & GeometryFormat.Prelit) > 0)
            writer.WriteStructureArray(colors, expectedSizeOfElement: 4);

        if ((worldFormat & (GeometryFormat.Textured | GeometryFormat.Textured2)) > 0)
            writer.WriteStructureArray(texCoords1, expectedSizeOfElement: 8);

        if ((worldFormat & GeometryFormat.Textured2) > 0)
            writer.WriteStructureArray(texCoords2, expectedSizeOfElement: 8);

        writer.WriteStructureArray(triangles, expectedSizeOfElement: 8);
    }
}
