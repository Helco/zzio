using System;
using System.Text;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
    [Serializable]
    public enum FOModelRenderType
    {
        Standard1 = 0,
        Additive1 = 1,
        EnvMap32 = 2,
        EnvMap64 = 3,
        EnvMap96 = 4, // Cathedral
        EnvMap128 = 5,
        EnvMap196 = 6, // London windows
        EnvMap255 = 7, // London cupboard windows
        Standard2 = 8, // Metallic?
        Standard3 = 9, // Plants?
        Additive2 = 10, // Cob webs 
        Additive3 = 11,

        Unknown = -1
    }

    [Serializable]
    public class FOModel : ISceneSection
    {
        public UInt32 idx;
        public string filename = "";
        public Vector pos, rot;
        public float f1, f2, f3, f4, f5;
        public IColor color;
        public byte worldDetailLevel, ff2;
        public FOModelRenderType renderType;
        public byte ff3;
        public Int32 i7;

        public void Read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            idx = reader.ReadUInt32();
            filename = reader.ReadZString();
            pos = Vector.ReadNew(reader);
            rot = Vector.ReadNew(reader);
            f1 = reader.ReadSingle();
            f2 = reader.ReadSingle();
            f3 = reader.ReadSingle();
            f4 = reader.ReadSingle();
            f5 = reader.ReadSingle();
            color = IColor.ReadNew(reader);
            worldDetailLevel = reader.ReadByte();
            ff2 = reader.ReadByte();
            renderType = EnumUtils.intToEnum<FOModelRenderType>(reader.ReadInt32());
            ff3 = reader.ReadByte();
            i7 = reader.ReadInt32();
        }

        public void Write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(idx);
            writer.WriteZString(filename);
            pos.Write(writer);
            rot.Write(writer);
            writer.Write(f1);
            writer.Write(f2);
            writer.Write(f3);
            writer.Write(f4);
            writer.Write(f5);
            color.Write(writer);
            writer.Write(worldDetailLevel);
            writer.Write(ff2);
            writer.Write((int)renderType);
            writer.Write(ff3);
            writer.Write(i7);
        }
    }
}
