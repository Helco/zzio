using System;
using System.IO;
using System.Numerics;

namespace zzio.rwbs
{
    [Serializable]
    public class RWAtomicSection : StructSection
    {
        public override SectionId sectionId => SectionId.AtomicSection;

        public UInt32 matIdBase;
        public Vector3 bbox1, bbox2;
        public Vector3[] vertices = new Vector3[0];
        public Normal[] normals = new Normal[0];
        public IColor[] colors = new IColor[0];
        public Vector2[]
            texCoords1 = new Vector2[0],
            texCoords2 = new Vector2[0];
        public VertexTriangle[] triangles = new VertexTriangle[0];

        protected override void readStruct(Stream stream)
        {
            var world = FindParentById(SectionId.World) as RWWorld;
            if (world == null)
                throw new InvalidDataException("RWAtomicSection has to be child of RWWorld");
            GeometryFormat worldFormat = world.format;

            using BinaryReader reader = new BinaryReader(stream);
            matIdBase = reader.ReadUInt32();
            triangles = new VertexTriangle[reader.ReadUInt32()];
            vertices = new Vector3[reader.ReadUInt32()];
            bbox1 = reader.ReadVector3();
            bbox2 = reader.ReadVector3();
            reader.ReadUInt32(); // unused
            reader.ReadUInt32();

            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = reader.ReadVector3();

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
                texCoords1 = new Vector2[vertices.Length];
                for (int i = 0; i < texCoords1.Length; i++)
                    texCoords1[i] = reader.ReadVector2();
            }

            if ((worldFormat & GeometryFormat.Textured2) > 0)
            {
                texCoords2 = new Vector2[vertices.Length];
                for (int i = 0; i < texCoords2.Length; i++)
                    texCoords2[i] = reader.ReadVector2();
            }

            for (int i = 0; i < triangles.Length; i++)
                triangles[i] = VertexTriangle.ReadNew(reader);
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
            writer.Write(bbox1);
            writer.Write(bbox2);
            writer.Write(0U);
            writer.Write(0U);

            Array.ForEach(vertices, writer.Write);

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
                Array.ForEach(texCoords1, writer.Write);
            }

            if ((worldFormat & GeometryFormat.Textured2) > 0)
            {
                Array.ForEach(texCoords2, writer.Write);
            }

            foreach (VertexTriangle t in triangles)
                t.Write(writer);
        }
    }
}
