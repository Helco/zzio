using System;
using System.IO;
using System.Numerics;
using zzio;

namespace zzio.scn;

[Serializable]
public struct DynModelData
{
    public float a1, a2, a3, a4, a5, a6, a7;
    public byte someFlag;
    public IColor someColor;
    public uint cc;
    public string s1, s2;
}

[Serializable]
public class DynModel : ISceneSection
{
    public uint idx, c1, c2;
    public Vector3 pos, rot;
    public float f1, f2;
    public Vector3 v1;
    public uint ii1, ii2;
    public DynModelData[] data = Array.Empty<DynModelData>(); //always three

    public void Read(Stream stream)
    {
        using BinaryReader reader = new(stream);
        idx = reader.ReadUInt32();
        c1 = reader.ReadUInt32();
        c2 = reader.ReadUInt32();
        pos = reader.ReadVector3();
        rot = reader.ReadVector3();
        f1 = reader.ReadSingle();
        f2 = reader.ReadSingle();
        v1 = reader.ReadVector3();
        ii1 = reader.ReadUInt32();
        ii2 = reader.ReadUInt32();
        data = new DynModelData[3];
        for (uint i = 0; i < 3; i++)
        {
            data[i].a1 = reader.ReadSingle();
            data[i].a2 = reader.ReadSingle();
            data[i].a3 = reader.ReadSingle();
            data[i].a4 = reader.ReadSingle();
            data[i].a5 = reader.ReadSingle();
            data[i].a6 = reader.ReadSingle();
            data[i].a7 = reader.ReadSingle();
            data[i].someFlag = reader.ReadByte();
            data[i].someColor = IColor.ReadNew(reader);
            data[i].cc = reader.ReadUInt32();
            data[i].s1 = reader.ReadZString();
            data[i].s2 = reader.ReadZString();
        }
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(idx);
        writer.Write(c1);
        writer.Write(c2);
        writer.Write(pos);
        writer.Write(rot);
        writer.Write(f1);
        writer.Write(f2);
        writer.Write(v1);
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
            data[i].someColor.Write(writer);
            writer.Write(data[i].cc);
            writer.WriteZString(data[i].s1);
            writer.WriteZString(data[i].s2);
        }
    }
}
