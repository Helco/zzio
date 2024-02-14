using System;
using System.IO;
using System.Numerics;

namespace zzio.rwbs;

[Serializable]
public class MorphTarget
{
    public Vector3 bsphereCenter;
    public float bsphereRadius;
    public Vector3[] vertices = [], normals = [];
}

[Serializable]
public class RWGeometry : StructSection
{
    public override SectionId sectionId => SectionId.Geometry;

    public GeometryFormat format;
    public float ambient, specular, diffuse;
    public IColor[] colors = [];
    public Vector2[][] texCoords = [];
    public VertexTriangle[] triangles = [];
    public MorphTarget[] morphTargets = [];

    protected override void readStruct(Stream stream)
    {
        using BinaryReader reader = new(stream);
        format = EnumUtils.intToFlags<GeometryFormat>(reader.ReadUInt32());
        triangles = new VertexTriangle[reader.ReadUInt32()];
        var vertexCount = reader.ReadInt32();
        morphTargets = new MorphTarget[reader.ReadUInt32()];
        ambient = reader.ReadSingle();
        specular = reader.ReadSingle();
        diffuse = reader.ReadSingle();

        if ((format & GeometryFormat.Native) == 0)
        {
            if ((format & GeometryFormat.Prelit) > 0)
                colors = reader.ReadStructureArray<IColor>(vertexCount, expectedSizeOfElement: 4);

            if ((format & (GeometryFormat.Textured | GeometryFormat.Textured2)) > 0)
            {
                int texCount = (((int)format) >> 16) & 0xff;
                if (texCount == 0)
                    texCount = ((format & GeometryFormat.Textured2) > 0 ? 2 : 1);

                texCoords = new Vector2[texCount][];
                for (int i = 0; i < texCount; i++)
                    texCoords[i] = reader.ReadStructureArray<Vector2>(vertexCount, expectedSizeOfElement: 8);
            }

            reader.ReadStructureArray(triangles, expectedSizeOfElement: 8);
            foreach (ref var t in triangles.AsSpan())
                t = t.ShuffledForGeometry; // Triangle members are ordered differently in RWGeometry...
        } // no native format

        for (int i = 0; i < morphTargets.Length; i++)
        {
            morphTargets[i] = new MorphTarget
            {
                bsphereCenter = reader.ReadVector3(),
                bsphereRadius = reader.ReadSingle()
            };
            bool hasVertices = reader.ReadUInt32() > 0;
            bool hasNormals = reader.ReadUInt32() > 0;
            if (hasVertices)
                morphTargets[i].vertices = reader.ReadStructureArray<Vector3>(vertexCount, expectedSizeOfElement: 12);
            if (hasNormals)
                morphTargets[i].normals = reader.ReadStructureArray<Vector3>(vertexCount, expectedSizeOfElement: 12);
        }
    }

    protected override void writeStruct(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(triangles.Length);
        int vertexCount = morphTargets.Length == 0 ? 0 : morphTargets[0].vertices.Length;
        writer.Write(vertexCount);
        writer.Write(morphTargets.Length);
        writer.Write(ambient);
        writer.Write(specular);
        writer.Write(diffuse);

        if ((format & GeometryFormat.Native) == 0)
        {
            if ((format & GeometryFormat.Prelit) > 0)
                writer.WriteStructureArray(colors, expectedSizeOfElement: 4);

            if ((format & (GeometryFormat.Textured | GeometryFormat.Textured2)) > 0)
            {
                int texCount = (((int)format) >> 16) & 0xff;
                if (texCount == 0)
                    texCount = ((format & GeometryFormat.Textured2) > 0 ? 2 : 1);

                for (int i = 0; i < texCount; i++)
                    writer.WriteStructureArray(texCoords[i], expectedSizeOfElement: 8);
            }

            var trianglesCopy = (VertexTriangle[])triangles.Clone();
            foreach (ref VertexTriangle t in trianglesCopy.AsSpan())
                t = t.ShuffledForGeometry;
            writer.WriteStructureArray(trianglesCopy, expectedSizeOfElement: 8);
        } // no native

        foreach (MorphTarget mt in morphTargets)
        {
            writer.Write(mt.bsphereCenter);
            writer.Write(mt.bsphereRadius);
            writer.Write((uint)(mt.vertices.Length == 0 ? 0 : 1));
            writer.Write((uint)(mt.normals.Length == 0 ? 0 : 1));
            writer.WriteStructureArray(mt.vertices, expectedSizeOfElement: 12);
            writer.WriteStructureArray(mt.normals, expectedSizeOfElement: 12);
        }
    }
}
