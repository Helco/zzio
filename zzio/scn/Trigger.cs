using System;
using System.IO;
using System.Numerics;

namespace zzio.scn;

[Serializable]
public enum TriggerColliderType
{
    Box = 0,
    Sphere = 1,
    Point = 2,

    Unknown = -1
}

[Serializable]
public class Trigger : ISceneSection
{
    public uint idx;
    public bool requiresLooking;
    public TriggerColliderType colliderType;
    public Vector3 dir;
    public TriggerType type;
    public uint ii1, ii2, ii3, ii4;
    public string s = "";
    public Vector3 // TODO: Move Collider out 
        pos,
        end; // only if type == Box
    public float radius; // only if type == Sphere

    public void Read(Stream stream)
    {
        using BinaryReader reader = new(stream);
        idx = reader.ReadUInt32();
        colliderType = EnumUtils.intToEnum<TriggerColliderType>(reader.ReadInt32());
        requiresLooking = reader.ReadUInt32() != 0;
        dir = reader.ReadVector3();
        type = EnumUtils.intToEnum<TriggerType>(reader.ReadInt32());
        ii1 = reader.ReadUInt32();
        ii2 = reader.ReadUInt32();
        ii3 = reader.ReadUInt32();
        ii4 = reader.ReadUInt32();
        s = reader.ReadZString();
        pos = reader.ReadVector3();
        end = new Vector3();
        radius = 0.0f;
        switch (colliderType)
        {
            case TriggerColliderType.Box:
                end = reader.ReadVector3();
                break;
            case TriggerColliderType.Sphere:
                radius = reader.ReadSingle();
                break;
            case TriggerColliderType.Point: break;
            default: { throw new InvalidDataException("Invalid trigger type"); }
        }
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(idx);
        writer.Write((int)colliderType);
        writer.Write(requiresLooking ? 1 : 0);
        writer.Write(dir);
        writer.Write((int)type);
        writer.Write(ii1);
        writer.Write(ii2);
        writer.Write(ii3);
        writer.Write(ii4);
        writer.WriteZString(s);
        writer.Write(pos);
        switch (colliderType)
        {
            case TriggerColliderType.Box:
                writer.Write(end);
                break;
            case TriggerColliderType.Sphere:
                writer.Write(radius);
                break;
        }
    }
    public Trigger Clone()
    {
        return (Trigger)this.MemberwiseClone();
    }
}
