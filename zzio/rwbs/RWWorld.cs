using System;
using System.IO;
using System.Numerics;
using zzio;

namespace zzio.rwbs;

[Serializable]
public class RWWorld : StructSection
{
    public override SectionId sectionId => SectionId.World;

    public bool rootIsWorldSector;
    public Vector3 origin;
    public float ambient, specular, diffuse;
    public uint
        numTriangles,
        numVertices,
        numPlaneSectors,
        numWorldSectors,
        colSectorSize;
    public GeometryFormat format;

    protected override void readStruct(Stream stream)
    {
        using BinaryReader reader = new(stream);
        rootIsWorldSector = reader.ReadUInt32() > 0;
        origin = reader.ReadVector3();
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
        using BinaryWriter writer = new(stream);
        writer.Write((uint)(rootIsWorldSector ? 1 : 0));
        writer.Write(origin);
        writer.Write(ambient);
        writer.Write(specular);
        writer.Write(diffuse);
        writer.Write(numTriangles);
        writer.Write(numVertices);
        writer.Write(numPlaneSectors);
        writer.Write(numWorldSectors);
        writer.Write(colSectorSize);
        writer.Write((uint)format);
    }
}
