using System;
using System.IO;

namespace zzio
{
    public enum ZZBuildCountry
    {
        Germany,
        England,
        France,
        Spain,
        Italy,
        Japanese,
        UnitedStates,
        Russia = 8,

        Unknown = -1
    }

    public enum ZZBuildType
    {
        Debug,
        Release,
        WebDemo,
        CDDemo,

        Unknown = -1
    }

    public record struct ZZVersion(
        string Author,
        ZZBuildCountry BuildCountry,
        ZZBuildType BuildType,
        uint Unknown1,
        uint BuildVersion,
        string Date,
        string Time,
        uint Year,
        uint Unknown2)
    {
        public static ZZVersion ReadNew(BinaryReader r) => new ZZVersion
        {
            Author = r.ReadZString(),
            BuildCountry = EnumUtils.intToEnum<ZZBuildCountry>(r.ReadInt32()),
            BuildType = EnumUtils.intToEnum<ZZBuildType>(r.ReadInt32()),
            Unknown1 = r.ReadUInt32(),
            BuildVersion = r.ReadUInt32(),
            Date = r.ReadZString(),
            Time = r.ReadZString(),
            Year = r.ReadUInt32(),
            Unknown2 = r.ReadUInt32()
        };

        public void Write(BinaryWriter w)
        {
            w.WriteZString(Author);
            w.Write((int)BuildCountry);
            w.Write((int)BuildType);
            w.Write(Unknown1);
            w.Write(BuildVersion);
            w.WriteZString(Date);
            w.WriteZString(Time);
            w.Write(Year);
            w.Write(Unknown2);
        }
    }
}
