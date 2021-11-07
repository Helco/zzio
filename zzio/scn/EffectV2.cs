using System;
using System.Text;
using System.IO;
using zzio.primitives;
using zzio.utils;

// TODO: Merge this to Effect 
namespace zzio.scn
{
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
        public UInt32 idx;
        public EffectV2Type type;
        public UInt32 i1, i2, i3, i4, i5;
        public Vector v1, v2, v3;
        public UInt32 param;
        public string s = "";

        public void Read(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            idx = reader.ReadUInt32();
            type = EnumUtils.intToEnum<EffectV2Type>(reader.ReadInt32());
            i1 = reader.ReadUInt32();
            i2 = reader.ReadUInt32();
            i3 = reader.ReadUInt32();
            i4 = reader.ReadUInt32();
            i5 = reader.ReadUInt32();
            v1 = v2 = v3 = new Vector();
            switch (type)
            {
                case (EffectV2Type.Unknown1):
                case (EffectV2Type.Unknown6):
                case (EffectV2Type.Unknown10):
                    param = reader.ReadUInt32();
                    v1 = Vector.ReadNew(reader);
                    v2 = Vector.ReadNew(reader);
                    break;
                case (EffectV2Type.Snowflakes):
                    param = reader.ReadUInt32();
                    break;
                case (EffectV2Type.Unknown13):
                    s = reader.ReadZString();
                    v1 = Vector.ReadNew(reader);
                    v2 = Vector.ReadNew(reader);
                    v3 = Vector.ReadNew(reader);
                    param = reader.ReadUInt32();
                    break;
                default: { throw new InvalidDataException("Invalid effect v2 type"); }
            }
        }

        public void Write(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
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
                    v1.Write(writer);
                    v2.Write(writer);
                    break;
                case EffectV2Type.Snowflakes:
                    writer.Write(param);
                    break;
                case EffectV2Type.Unknown13:
                    writer.WriteZString(s);
                    v1.Write(writer);
                    v2.Write(writer);
                    v3.Write(writer);
                    writer.Write(param);
                    break;
            }
        }
    }
}
