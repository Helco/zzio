using System;
using System.IO;
using zzio.utils;
using zzio.primitives;

namespace zzio.rwbs
{
    [Serializable]
    public class RWWorld : StructSection
    {
        public override SectionId sectionId => SectionId.World;

        public bool rootIsWorldSector;
        public Vector origin;
        public float ambient, specular, diffuse;
        public UInt32
            numTriangles,
            numVertices,
            numPlaneSectors,
            numWorldSectors,
            colSectorSize;
        public GeometryFormat format;

        protected override void readStruct(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            rootIsWorldSector = reader.ReadUInt32() > 0;
            origin = Vector.ReadNew(reader);
            ambient = reader.ReadSingle();
            specular = reader.ReadSingle();
            diffuse = reader.ReadSingle();
            numTriangles = reader.ReadUInt32();
            numVertices = reader.ReadUInt32();
            numPlaneSectors = reader.ReadUInt32();
            numWorldSectors = reader.ReadUInt32();
            colSectorSize = reader.ReadUInt32();
            format = EnumUtils.intToFlags<GeometryFormat>(reader.ReadUInt32());
        }

        protected override void writeStruct(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write((UInt32)(rootIsWorldSector ? 1 : 0));
            origin.Write(writer);
            writer.Write(ambient);
            writer.Write(specular);
            writer.Write(diffuse);
            writer.Write(numTriangles);
            writer.Write(numVertices);
            writer.Write(numPlaneSectors);
            writer.Write(numWorldSectors);
            writer.Write(colSectorSize);
            writer.Write((UInt32)format);
        }
    }
}
