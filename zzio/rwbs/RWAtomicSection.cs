using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;
using zzio.primitives;

namespace zzio.rwbs
{
    [Serializable]
    public class RWAtomicSection : StructSection
    {
        public override SectionId sectionId => SectionId.AtomicSection;

        public UInt32 matIdBase;
        public Vector bbox1, bbox2;
        public Vector[] vertices = new Vector[0];
        public Normal[] normals = new Normal[0];
        public IColor[] colors = new IColor[0];
        public TexCoord[]
            texCoords1 = new TexCoord[0],
            texCoords2 = new TexCoord[0];
        public Triangle[] triangles = new Triangle[0];

        protected override void readStruct(Stream stream)
        {
            var world = FindParentById(SectionId.World) as RWWorld;
            if (world == null)
                throw new InvalidDataException("RWAtomicSection has to be child of RWWorld");
            GeometryFormat worldFormat = world.format;

            using BinaryReader reader = new BinaryReader(stream);
            matIdBase = reader.ReadUInt32();
            triangles = new Triangle[reader.ReadUInt32()];
            vertices = new Vector[reader.ReadUInt32()];
            bbox1 = Vector.ReadNew(reader);
            bbox2 = Vector.ReadNew(reader);
            reader.ReadUInt32(); // unused
            reader.ReadUInt32();

            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = Vector.ReadNew(reader);

            if ((worldFormat & GeometryFormat.Normals) > 0)
            {
                normals = new Normal[vertices.Length];
                for (int i = 0; i < normals.Length; i++)
                    normals[i] = Normal.ReadNew(reader);
            }

            if ((worldFormat & GeometryFormat.Prelit) > 0)
            {
                colors = new IColor[vertices.Length];
                for (int i = 0; i < colors.Length; i++)
                    colors[i] = IColor.ReadNew(reader);   
            }

            if ((worldFormat & GeometryFormat.Textured) > 0 ||
                (worldFormat & GeometryFormat.Textured2) > 0)
            {
                texCoords1 = new TexCoord[vertices.Length];
                for (int i = 0; i < texCoords1.Length; i++)
                    texCoords1[i] = TexCoord.ReadNew(reader);
            }

            if ((worldFormat & GeometryFormat.Textured2) > 0)
            {
                texCoords2 = new TexCoord[vertices.Length];
                for (int i = 0; i < texCoords2.Length; i++)
                    texCoords2[i] = TexCoord.ReadNew(reader);
            }

            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = Triangle.ReadNew(reader);
        }

        protected override void writeStruct(Stream stream)
        {
            var world = FindParentById(SectionId.World) as RWWorld;
            if (world == null)
                throw new InvalidDataException("RWAtomicSection has to be child of RWWorld");
            GeometryFormat worldFormat = world.format;

            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(matIdBase);
            writer.Write(triangles.Length);
            writer.Write(vertices.Length);
            bbox1.Write(writer);
            bbox2.Write(writer);
            writer.Write(0U);
            writer.Write(0U);
            
            foreach (Vector v in vertices)
                v.Write(writer);

            if ((worldFormat & GeometryFormat.Normals) > 0)
            {
                foreach (Normal n in normals)
                    n.Write(writer);
            }

            if ((worldFormat & GeometryFormat.Prelit) > 0)
            {
                foreach (IColor c in colors)
                    c.Write(writer);
            }

            if ((worldFormat & GeometryFormat.Textured) > 0 ||
                (worldFormat & GeometryFormat.Textured2) > 0)
            {
                foreach (TexCoord t in texCoords1)
                    t.Write(writer);
            }

            if ((worldFormat & GeometryFormat.Textured2) > 0)
            {
                foreach (TexCoord t in texCoords2)
                    t.Write(writer);
            }

            foreach (Triangle t in triangles)
                t.Write(writer);
        }
    }
}
