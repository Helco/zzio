using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;
using zzio.primitives;

namespace zzio.rwbs
{
    [Serializable]
    public struct MorphTarget
    {
        public Vector bsphereCenter;
        public float bsphereRadius;
        public Vector[] vertices, normals;
    }

    [Serializable]
    public class RWGeometry : StructSection
    {
        public override SectionId sectionId => SectionId.Geometry;

        public GeometryFormat format;
        public float ambient, specular, diffuse;
        public IColor[] colors = new IColor[0];
        public TexCoord[][] texCoords = new TexCoord[0][];
        public Triangle[] triangles = new Triangle[0];
        public MorphTarget[] morphTargets = new MorphTarget[0];

        protected override void readStruct(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            format = EnumUtils.intToFlags<GeometryFormat>(reader.ReadUInt32());
            triangles = new Triangle[reader.ReadUInt32()];
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
                    
                    texCoords = new TexCoord[texCount][];
                    for (int i = 0; i < texCount; i++)
                    {
                        texCoords[i] = new TexCoord[vertexCount];
                        for (int j = 0; j < vertexCount; j++)
                            texCoords[i][j] = TexCoord.ReadNew(reader);
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
                morphTargets[i].bsphereCenter = Vector.ReadNew(reader);
                morphTargets[i].bsphereRadius = reader.ReadSingle();
                morphTargets[i].vertices = new Vector[0];
                morphTargets[i].normals = new Vector[0];
                bool hasVertices = reader.ReadUInt32() > 0;
                bool hasNormals = reader.ReadUInt32() > 0;
                if (hasVertices)
                {
                    morphTargets[i].vertices = new Vector[vertexCount];
                    for (uint j = 0; j < vertexCount; j++)
                        morphTargets[i].vertices[j] = Vector.ReadNew(reader);
                }
                if (hasNormals)
                {
                    morphTargets[i].normals = new Vector[vertexCount];
                    for (uint j = 0; j < vertexCount; j++)
                        morphTargets[i].normals[j] = Vector.ReadNew(reader);
                }
            }
        }

        protected override void writeStruct(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
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
                            texCoords[i][j].Write(writer);
                    }
                }

                foreach (Triangle t in triangles)
                {
                    writer.Write(t.v2);
                    writer.Write(t.v1);
                    writer.Write(t.m);
                    writer.Write(t.v3);
                }
            } // no native

            foreach (MorphTarget mt in morphTargets)
            {
                mt.bsphereCenter.Write(writer);
                writer.Write(mt.bsphereRadius);
                writer.Write((UInt32)(mt.vertices.Length == 0 ? 0 : 1));
                writer.Write((UInt32)(mt.normals.Length == 0 ? 0 : 1));
                foreach (Vector v in mt.vertices)
                    v.Write(writer);
                foreach (Vector n in mt.normals)
                    n.Write(writer);
            }
        }
    }
}
