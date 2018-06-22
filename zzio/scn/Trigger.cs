using System;
using System.Text;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
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
        public UInt32 idx, normalizeDir;
        public TriggerColliderType colliderType;
        public Vector dir;
        public TriggerType type;
        public UInt32 ii1, ii2, ii3, ii4;
        public string s;
        public Vector // TODO: Move Collider out
            pos,
            size; // only if type == Box
        public float radius; // only if type == Sphere

        public void read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            idx = reader.ReadUInt32();
            colliderType = EnumUtils.intToEnum<TriggerColliderType>(reader.ReadInt32());
            normalizeDir = reader.ReadUInt32();
            dir = Vector.read(reader);
            type = EnumUtils.intToEnum<TriggerType>(reader.ReadInt32());
            ii1 = reader.ReadUInt32();
            ii2 = reader.ReadUInt32();
            ii3 = reader.ReadUInt32();
            ii4 = reader.ReadUInt32();
            s = reader.ReadZString();
            pos = Vector.read(reader);
            size = new Vector();
            radius = 0.0f;
            switch (colliderType)
            {
                case TriggerColliderType.Box:
                    size = Vector.read(reader);
                    break;
                case TriggerColliderType.Sphere:
                    radius = reader.ReadSingle();
                    break;
                case TriggerColliderType.Point: break;
                default: { throw new InvalidDataException("Invalid trigger type"); }
            }
        }

        public void write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(idx);
            writer.Write((int)colliderType);
            writer.Write(normalizeDir);
            dir.write(writer);
            writer.Write((int)type);
            writer.Write(ii1);
            writer.Write(ii2);
            writer.Write(ii3);
            writer.Write(ii4);
            writer.WriteZString(s);
            pos.write(writer);
            switch (colliderType)
            {
                case TriggerColliderType.Box:
                    size.write(writer);
                    break;
                case TriggerColliderType.Sphere:
                    writer.Write(radius);
                    break;
            }
        }
    }
}
