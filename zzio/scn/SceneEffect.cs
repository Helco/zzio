using System;
using System.IO;
using System.Numerics;

namespace zzio.scn;

public enum SceneEffectType
{
    // UnusedX means the original game knows how to read the effects
    // but does not instantiate them so as long as they do not
    // bother the code we will read them as well but nothing more

    Leaves = 1,
    Unused4 = 4,
    Unused5 = 5,
    Unknown6 = 6,
    Unused7 = 7,
    Unknown10 = 10,
    Snowflakes = 11,
    Combiner = 13,

    Unknown = -1
}

public enum SceneEffectReadVersion
{
    V1,
    V2
}

public enum SceneEffectOrder
{
    Early,
    Solid,
    Late
}

public class SceneEffect(SceneEffectReadVersion readVersion) : ISceneSection
{
    public SceneEffectReadVersion ReadVersion => readVersion;
    public uint idx;
    public SceneEffectType type;
    public Vector3
        pos,
        end, // pos-to-end form a ColliderBox, only for Leaves, Unused5, Unknown6, Unknown10
        dir, // only for Combiner
        up; // only for combiner
    public uint param; // particle base count (or unknown)
    public string effectFile = "";
    public SceneEffectOrder order = SceneEffectOrder.Late;

    public void Read(Stream stream)
    {
        using BinaryReader reader = new(stream);
        idx = reader.ReadUInt32();
        type = EnumUtils.intToEnum<SceneEffectType>(reader.ReadInt32());
        if (readVersion is SceneEffectReadVersion.V2)
        {
            order = EnumUtils.intToEnum<SceneEffectOrder>(reader.ReadInt32());
            reader.BaseStream.Position += 16; // completly ignored data
        }

        param = 0;
        switch (type)
        {
            case SceneEffectType.Leaves:
            case SceneEffectType.Unused5:
            case SceneEffectType.Unknown6:
            case SceneEffectType.Unknown10:
                param = reader.ReadUInt32();
                pos = reader.ReadVector3();
                end = reader.ReadVector3();
                break;
            case SceneEffectType.Snowflakes:
                param = reader.ReadUInt32();
                break;
            case SceneEffectType.Combiner:
                effectFile = reader.ReadZString();
                up = reader.ReadVector3();
                dir = reader.ReadVector3();
                pos = reader.ReadVector3();
                param = reader.ReadUInt32();
                break;
            case SceneEffectType.Unused4:
                param = reader.ReadUInt32();
                pos = reader.ReadVector3();
                break;
            case SceneEffectType.Unused7:
                effectFile = reader.ReadZString();
                pos = reader.ReadVector3();
                break;
            default: { throw new InvalidDataException("Invalid scene effect type"); }
        }
    }

    /// <remarks>Always written as V2</remarks>
    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(idx);
        writer.Write((int)type);
        if (readVersion is SceneEffectReadVersion.V2)
        {
            writer.Write((int)order);
            writer.Write(Vector4.Zero); // ignored data
        }

        switch (type)
        {
            case SceneEffectType.Leaves:
            case SceneEffectType.Unused5:
            case SceneEffectType.Unknown6:
            case SceneEffectType.Unknown10:
                writer.Write(param);
                writer.Write(pos);
                writer.Write(end);
                break;
            case SceneEffectType.Snowflakes:
                writer.Write(param);
                break;
            case SceneEffectType.Combiner:
                writer.WriteZString(effectFile);
                writer.Write(up);
                writer.Write(dir);
                writer.Write(pos);
                writer.Write(param);
                break;
            case SceneEffectType.Unused4:
                writer.Write(param);
                writer.Write(pos);
                break;
            case SceneEffectType.Unused7:
                writer.WriteZString(effectFile);
                writer.Write(pos);
                break;
        }
    }
}
