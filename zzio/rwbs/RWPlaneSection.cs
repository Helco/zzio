using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using zzio.utils;
using zzio.primitives;

namespace zzio.rwbs
{
    public enum RWPlaneSectionType
    {
        // *Plane has a *normal* vector in * direction

        XPlane = 0,
        YPlane = 4,
        ZPlane = 8,

        Unknown = -1
    }

    public static class RWPlaneSectionTypeExtensions
    {
        public static Vector AsNormal(this RWPlaneSectionType t) => t switch
        {
            RWPlaneSectionType.XPlane => new Vector(1.0f, 0.0f, 0.0f),
            RWPlaneSectionType.YPlane => new Vector(0.0f, 1.0f, 0.0f),
            RWPlaneSectionType.ZPlane => new Vector(0.0f, 0.0f, 1.0f),
            _ => throw new NotImplementedException("Unknown plane section type")
        };

        public static int ToIndex(this RWPlaneSectionType t) => ((int)t) / 4;
    }

    public class RWPlaneSection : StructSection
    {
        public override SectionId sectionId => SectionId.PlaneSection;

        public RWPlaneSectionType sectorType; // unknown enum
        public float centerValue, leftValue, rightValue;
        public bool leftIsWorldSector, rightIsWorldSector;

        protected override void readStruct(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            sectorType = EnumUtils.intToEnum<RWPlaneSectionType>(reader.ReadInt32());
            centerValue = reader.ReadSingle();
            leftIsWorldSector = reader.ReadUInt32() > 0;
            rightIsWorldSector = reader.ReadUInt32() > 0;
            leftValue = reader.ReadSingle();
            rightValue = reader.ReadSingle();
        }

        protected override void writeStruct(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write((int)sectorType);
            writer.Write(centerValue);
            writer.Write((UInt32)(leftIsWorldSector ? 1 : 0));
            writer.Write((UInt32)(rightIsWorldSector ? 1 : 0));
            writer.Write(leftValue);
            writer.Write(rightValue);
        }
    }
}
