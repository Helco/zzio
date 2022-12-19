using System;
using System.IO;

namespace zzio.scn
{
    [System.Serializable]
    public enum VersionBuildType
    {
        Debug = 0,
        Release = 1,
        WebDemo = 2,
        CdDemo = 3,

        Unknown = -1
    }

    [System.Serializable]
    public enum VersionBuildCountry
    {
        Germany = 0,
        English = 1,
        France = 2,
        Spain = 3,
        Italy = 4,
        Japanese = 5,
        English6 = 6,
        Russia = 8,

        Unknown = -1
    }

    [System.Serializable]
    public class Version : ISceneSection
    {
        public string author = "";
        public VersionBuildCountry country;
        public VersionBuildType type;
        public uint v3, buildVersion;
        public string date = "", time = "";
        public uint year, vv2;

        public void Read(Stream stream)
        {
            using BinaryReader reader = new(stream);
            author = reader.ReadZString();
            country = EnumUtils.intToEnum<VersionBuildCountry>(reader.ReadInt32());
            type = EnumUtils.intToEnum<VersionBuildType>(reader.ReadInt32());
            v3 = reader.ReadUInt32();
            buildVersion = reader.ReadUInt32();
            date = reader.ReadZString();
            time = reader.ReadZString();
            year = reader.ReadUInt32();
            vv2 = reader.ReadUInt32();
        }

        public void Write(Stream stream)
        {
            using BinaryWriter writer = new(stream);
            writer.WriteZString(author);
            writer.Write((int)country);
            writer.Write((int)type);
            writer.Write(v3);
            writer.Write(buildVersion);
            writer.WriteZString(date);
            writer.WriteZString(time);
            writer.Write(year);
            writer.Write(vv2);
        }
    }
}
