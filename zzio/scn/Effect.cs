using System;
using System.Text;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
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
        public UInt32 idx;
        public EffectType type;
        public Vector v1, v2, v3;
        public UInt32 param;
        public string effectFile = "";

        public void Read(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            idx = reader.ReadUInt32();
            type = EnumUtils.intToEnum<EffectType>(reader.ReadInt32());
            v1 = v2 = v3 = new Vector();
            param = 0;
            switch (type)
            {
                case EffectType.Unknown1:
                case EffectType.Unknown5:
                case EffectType.Unknown6:
                case EffectType.Unknown10:
                    param = reader.ReadUInt32();
                    v1 = Vector.ReadNew(reader);
                    v2 = Vector.ReadNew(reader);
                    break;
                case EffectType.Unknown4:
                    param = reader.ReadUInt32();
                    v1 = Vector.ReadNew(reader);
                    break;
                case EffectType.Unknown7:
                    effectFile = reader.ReadZString();
                    v1 = Vector.ReadNew(reader);
                    break;
                case EffectType.Unknown13:
                    effectFile = reader.ReadZString();
                    v1 = Vector.ReadNew(reader);
                    v2 = Vector.ReadNew(reader);
                    v3 = Vector.ReadNew(reader);
                    param = reader.ReadUInt32();
                    break;
                default: { throw new InvalidDataException("Invalid effect type"); }
            }
        }

        public void Write(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(idx);
            writer.Write((int)type);
            switch (type)
            {
                case EffectType.Unknown1:
                case EffectType.Unknown5:
                case EffectType.Unknown6:
                case EffectType.Unknown10:
                    writer.Write(param);
                    v1.Write(writer);
                    v2.Write(writer);
                    break;
                case EffectType.Unknown4:
                    writer.Write(param);
                    v1.Write(writer);
                    break;
                case EffectType.Unknown7:
                    writer.WriteZString(effectFile);
                    v1.Write(writer);
                    break;
                case EffectType.Unknown13:
                    writer.WriteZString(effectFile);
                    v1.Write(writer);
                    v2.Write(writer);
                    v3.Write(writer);
                    writer.Write(param);
                    break;
            }
        }
    }
}
