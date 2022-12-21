using System;
using System.IO;
using System.Numerics;
using zzio;

// TODO: Merge this to Effect 
namespace zzio.scn;

[Serializable]
public enum EffectV2Type
{
    Unknown1 = 1,
    Unknown6 = 6,
    Unknown10 = 10,
    Snowflakes = 11,
    Unknown13 = 13,

    Unknown = -1
}

[Serializable]
public class EffectV2 : ISceneSection
{
    public uint idx;
    public EffectV2Type type;
    public uint i1, i2, i3, i4, i5;
    public Vector3 v1, v2, v3;
    public uint param;
    public string s = "";

    public void Read(Stream stream)
    {
        using BinaryReader reader = new(stream);
        idx = reader.ReadUInt32();
        type = EnumUtils.intToEnum<EffectV2Type>(reader.ReadInt32());
        i1 = reader.ReadUInt32();
        i2 = reader.ReadUInt32();
        i3 = reader.ReadUInt32();
        i4 = reader.ReadUInt32();
        i5 = reader.ReadUInt32();
        v1 = v2 = v3 = new Vector3();
        switch (type)
        {
            case (EffectV2Type.Unknown1):
            case (EffectV2Type.Unknown6):
            case (EffectV2Type.Unknown10):
                param = reader.ReadUInt32();
                v1 = reader.ReadVector3();
                v2 = reader.ReadVector3();
                break;
            case (EffectV2Type.Snowflakes):
                param = reader.ReadUInt32();
                break;
            case (EffectV2Type.Unknown13):
                s = reader.ReadZString();
                v1 = reader.ReadVector3();
                v2 = reader.ReadVector3();
                v3 = reader.ReadVector3();
                param = reader.ReadUInt32();
                break;
            default: { throw new InvalidDataException("Invalid effect v2 type"); }
        }
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(idx);
        writer.Write((int)type);
        writer.Write(i1);
        writer.Write(i2);
        writer.Write(i3);
        writer.Write(i4);
        writer.Write(i5);
        switch (type)
        {
            case EffectV2Type.Unknown1:
            case EffectV2Type.Unknown6:
            case EffectV2Type.Unknown10:
                writer.Write(param);
                writer.Write(v1);
                writer.Write(v2);
                break;
            case EffectV2Type.Snowflakes:
                writer.Write(param);
                break;
            case EffectV2Type.Unknown13:
                writer.WriteZString(s);
                writer.Write(v1);
                writer.Write(v2);
                writer.Write(v3);
                writer.Write(param);
                break;
        }
    }
}
