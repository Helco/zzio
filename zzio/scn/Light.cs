using System;
using System.IO;
using System.Numerics;
using zzio;

namespace zzio.scn;

public enum LightType
{
    Directional = 1,
    Ambient = 2,
    Point = 128,
    Spot = 129,

    Unknown = -1
}

[Flags]
public enum LightFlags
{
    LightAtomics = 1 << 0,
    LightWorld = 1 << 1
}

[Serializable]
public class Light : ISceneSection
{
    public uint idx;
    public LightType type;
    public FColor color;
    public LightFlags flags;
    public Vector3 pos, vec; // vec is either dir or a lookAt point
    public float radius;

    public void Read(Stream stream)
    {
        using BinaryReader reader = new(stream);
        idx = reader.ReadUInt32();
        type = EnumUtils.intToEnum<LightType>(reader.ReadInt32());
        color = FColor.ReadNew(reader);
        flags = EnumUtils.intToFlags<LightFlags>(reader.ReadUInt32());
        pos = new Vector3();
        vec = new Vector3();
        radius = 0.0f;
        switch (type)
        {
            case LightType.Directional:
                pos = reader.ReadVector3();
                vec = reader.ReadVector3();
                break;
            case LightType.Point:
                radius = reader.ReadSingle();
                pos = reader.ReadVector3();
                break;
            case LightType.Spot:
                radius = reader.ReadUInt32();
                pos = reader.ReadVector3();
                vec = reader.ReadVector3();
                break;
        }
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(idx);
        writer.Write((int)type);
        color.Write(writer);
        writer.Write((uint)flags);
        switch (type)
        {
            case LightType.Directional:
                writer.Write(pos);
                writer.Write(vec);
                break;
            case LightType.Point:
                writer.Write(radius);
                writer.Write(pos);
                break;
            case LightType.Spot:
                writer.Write(radius);
                writer.Write(pos);
                writer.Write(vec);
                break;
        }
    }
}