using System;
using System.IO;
using System.Numerics;
using zzio;

namespace zzio.rwbs
{
    [Serializable]
    public class MorphTarget
    {
        public Vector3 bsphereCenter;
        public float bsphereRadius;
        public Vector3[] vertices = new Vector3[0], normals = new Vector3[0];
    }

    [Serializable]
    public class RWGeometry : StructSection
    {
        public override SectionId sectionId => SectionId.Geometry;

        public GeometryFormat format;
        public float ambient, specular, diffuse;
        public IColor[] colors = new IColor[0];
        public Vector2[][] texCoords = new Vector2[0][];
        public VertexTriangle[] triangles = new VertexTriangle[0];
        public MorphTarget[] morphTargets = new MorphTarget[0];

        protected override void readStruct(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            format = EnumUtils.intToFlags<GeometryFormat>(reader.ReadUInt32());
            triangles = new VertexTriangle[reader.ReadUInt32()];
            UInt32 vertexCount = reader.ReadUInt32();
            morphTargets = new MorphTarget[reader.ReadUInt32()];
            ambient = reader.ReadSingle();
            specular = reader.ReadSingle();
            diffuse = reader.ReadSingle();

            if ((format & GeometryFormat.Native) == 0)
            {
                if ((format & GeometryFormat.Prelit) > 0)
                {
                    colors = new IColor[vertexCount];
                    for (int i = 0; i < colors.Length; i++)
                        colors[i] = IColor.ReadNew(reader);
                }

                if ((format & GeometryFormat.Textured) > 0 ||
                    (format & GeometryFormat.Textured2) > 0)
                {
                    int texCount = (((int)format) >> 16) & 0xff;
                    if (texCount == 0)
                        texCount = ((format & GeometryFormat.Textured2) > 0 ? 2 : 1);
                    
                    texCoords = new Vector2[texCount][];
                    for (int i = 0; i < texCount; i++)
                    {
                        texCoords[i] = new Vector2[vertexCount];
                        for (int j = 0; j < vertexCount; j++)
                            texCoords[i][j] = reader.ReadVector2();
                    }
                }

                for (int i = 0; i < triangles.Length; i++)
                {
                    // Triangle members are ordered differently in RWGeometry...
                    triangles[i].v2 = reader.ReadUInt16();
                    triangles[i].v1 = reader.ReadUInt16();
                    triangles[i].m = reader.ReadUInt16();
                    triangles[i].v3 = reader.ReadUInt16();
                }
            } // no native format

            for (int i = 0; i < morphTargets.Length; i++) {
                morphTargets[i] = new MorphTarget();
                morphTargets[i].bsphereCenter = reader.ReadVector3();
                morphTargets[i].bsphereRadius = reader.ReadSingle();
                morphTargets[i].vertices = new Vector3[0];
                morphTargets[i].normals = new Vector3[0];
                bool hasVertices = reader.ReadUInt32() > 0;
                bool hasNormals = reader.ReadUInt32() > 0;
                if (hasVertices)
                {
                    morphTargets[i].vertices = new Vector3[vertexCount];
                    for (uint j = 0; j < vertexCount; j++)
                        morphTargets[i].vertices[j] = reader.ReadVector3();
                }
                if (hasNormals)
                {
                    morphTargets[i].normals = new Vector3[vertexCount];
                    for (uint j = 0; j < vertexCount; j++)
                        morphTargets[i].normals[j] = reader.ReadVector3();
                }
            }
        }

        protected override void writeStruct(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
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
                {
                    foreach (IColor c in colors)
                        c.Write(writer);
                }

                if ((format & GeometryFormat.Textured) > 0 ||
                    (format & GeometryFormat.Textured2) > 0)
                {
                    int texCount = (((int)format) >> 16) & 0xff;
                    if (texCount == 0)
                        texCount = ((format & GeometryFormat.Textured2) > 0 ? 2 : 1);

                    for (int i = 0; i < texCount; i++)
                    {
                        for (int j = 0; j < vertexCount; j++)
                            writer.Write(texCoords[i][j]);
                    }
                }

                foreach (VertexTriangle t in triangles)
                {
                    writer.Write(t.v2);
                    writer.Write(t.v1);
                    writer.Write(t.m);
                    writer.Write(t.v3);
                }
            } // no native

            foreach (MorphTarget mt in morphTargets)
            {
                writer.Write(mt.bsphereCenter);
                writer.Write(mt.bsphereRadius);
                writer.Write((UInt32)(mt.vertices.Length == 0 ? 0 : 1));
                writer.Write((UInt32)(mt.normals.Length == 0 ? 0 : 1));
                Array.ForEach(mt.vertices, writer.Write);
                Array.ForEach(mt.normals, writer.Write);
            }
        }
    }
}
