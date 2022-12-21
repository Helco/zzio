using System;
using System.IO;
using System.Numerics;
using zzio;

namespace zzio.scn;

[Serializable]
public enum EffectType
{
    Unknown1 = 1,
    Unknown4 = 4,
    Unknown5 = 5,
    Unknown6 = 6,
    Unknown7 = 7,
    Unknown10 = 10,
    Unknown13 = 13,

    Unknown = -1
}

[Serializable]
public class Effect : ISceneSection
{
    public uint idx;
    public EffectType type;
    public Vector3 v1, v2, v3;
    public uint param;
    public string effectFile = "";

    public void Read(Stream stream)
    {
        using BinaryReader reader = new(stream);
        idx = reader.ReadUInt32();
        type = EnumUtils.intToEnum<EffectType>(reader.ReadInt32());
        v1 = v2 = v3 = new Vector3();
        param = 0;
        switch (type)
        {
            case EffectType.Unknown1:
            case EffectType.Unknown5:
            case EffectType.Unknown6:
            case EffectType.Unknown10:
                param = reader.ReadUInt32();
                v1 = reader.ReadVector3();
                v2 = reader.ReadVector3();
                break;
            case EffectType.Unknown4:
                param = reader.ReadUInt32();
                v1 = reader.ReadVector3();
                break;
            case EffectType.Unknown7:
                effectFile = reader.ReadZString();
                v1 = reader.ReadVector3();
                break;
            case EffectType.Unknown13:
                effectFile = reader.ReadZString();
                v1 = reader.ReadVector3();
                v2 = reader.ReadVector3();
                v3 = reader.ReadVector3();
                param = reader.ReadUInt32();
                break;
            default: { throw new InvalidDataException("Invalid effect type"); }
        }
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(idx);
        writer.Write((int)type);
        switch (type)
        {
            case EffectType.Unknown1:
            case EffectType.Unknown5:
            case EffectType.Unknown6:
            case EffectType.Unknown10:
                writer.Write(param);
                writer.Write(v1);
                writer.Write(v2);
                break;
            case EffectType.Unknown4:
                writer.Write(param);
                writer.Write(v1);
                break;
            case EffectType.Unknown7:
                writer.WriteZString(effectFile);
                writer.Write(v1);
                break;
            case EffectType.Unknown13:
                writer.WriteZString(effectFile);
                writer.Write(v1);
                writer.Write(v2);
                writer.Write(v3);
                writer.Write(param);
                break;
        }
    }
}
