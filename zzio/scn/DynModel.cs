using System;
using System.Text;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
    [Serializable]
    public struct DynModelData
    {
        public float a1, a2, a3, a4, a5, a6, a7;
        public byte someFlag;
        public IColor someColor;
        public UInt32 cc;
        public string s1, s2;
    }

    [Serializable]
    public class DynModel : ISceneSection
    {
        public UInt32 idx, c1, c2;
        public Vector pos, rot;
        public float f1, f2;
        public Vector v1;
        public UInt32 ii1, ii2;
        public DynModelData[] data; //always three

        public void read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            idx = reader.ReadUInt32();
            c1 = reader.ReadUInt32();
            c2 = reader.ReadUInt32();
            pos = Vector.read(reader);
            rot = Vector.read(reader);
            f1 = reader.ReadSingle();
            f2 = reader.ReadSingle();
            v1 = Vector.read(reader);
            ii1 = reader.ReadUInt32();
            ii2 = reader.ReadUInt32();
            data = new DynModelData[3];
            for (UInt32 i = 0; i < 3; i++)
            {
                data[i].a1 = reader.ReadSingle();
                data[i].a2 = reader.ReadSingle();
                data[i].a3 = reader.ReadSingle();
                data[i].a4 = reader.ReadSingle();
                data[i].a5 = reader.ReadSingle();
                data[i].a6 = reader.ReadSingle();
                data[i].a7 = reader.ReadSingle();
                data[i].someFlag = reader.ReadByte();
                data[i].someColor = IColor.read(reader);
                data[i].cc = reader.ReadUInt32();
                data[i].s1 = reader.ReadZString();
                data[i].s2 = reader.ReadZString();
            }
        }

        public void write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(idx);
            writer.Write(c1);
            writer.Write(c2);
            pos.write(writer);
            rot.write(writer);
            writer.Write(f1);
            writer.Write(f2);
            v1.write(writer);
            writer.Write(ii1);
            writer.Write(ii2);
            for (int i = 0; i < 3; i++)
            {
                writer.Write(data[i].a1);
                writer.Write(data[i].a2);
                writer.Write(data[i].a3);
                writer.Write(data[i].a4);
                writer.Write(data[i].a5);
                writer.Write(data[i].a6);
                writer.Write(data[i].a7);
                writer.Write(data[i].someFlag);
                data[i].someColor.write(writer);
                writer.Write(data[i].cc);
                writer.WriteZString(data[i].s1);
                writer.WriteZString(data[i].s2);
            }
        }
    }
}
