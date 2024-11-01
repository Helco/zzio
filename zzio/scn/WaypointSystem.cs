using System;
using System.IO;
using System.Diagnostics;
using System.Numerics;

// This surely needs some reverse engineering work being done...
namespace zzio.scn;

[Serializable]
public struct WaypointInnerData
{
    public uint iiv2;
    public uint[] data;
}

[Serializable]
public struct WaypointData
{
    public uint ii1, ii1ext, iiv2;
    public Vector3 v1;
    public uint[] innerdata1, innerdata2;
    public uint[]? inner3data1;
}

[Serializable]
public class WaypointSystem : ISceneSection
{
    public uint version;
    public byte[] data = new byte[0x18];
    public WaypointData[] waypointData = [];
    public WaypointInnerData[] inner2data1 = [];

    public void Read(Stream stream)
    {
        using BinaryReader reader = new(stream);
        version = reader.ReadUInt32();
        uint mustBeZero = reader.ReadUInt32();
        if (mustBeZero != 0)
            throw new InvalidDataException("Waypoint system start magic is not correct");

        if (version >= 5)
            data = reader.ReadBytes(0x18);
        uint count1 = reader.ReadUInt32();
        WaypointData[] d = new WaypointData[count1];
        for (uint i = 0; i < count1; i++)
        {
            d[i].ii1 = reader.ReadUInt32();
            if (version >= 4)
                d[i].ii1ext = reader.ReadUInt32();
            d[i].v1 = reader.ReadVector3();

            d[i].innerdata1 = reader.ReadStructureArray<uint>(reader.ReadInt32(), 4);
            d[i].innerdata2 = reader.ReadStructureArray<uint>(reader.ReadInt32(), 4);
        }

        if (version >= 2)
        {
            uint count2 = reader.ReadUInt32();
            inner2data1 = new WaypointInnerData[count2];
            for (uint j = 0; j < count2; j++)
            {
                inner2data1[j].iiv2 = reader.ReadUInt32();
                inner2data1[j].data = reader.ReadStructureArray<uint>(reader.ReadInt32(), 4);
            }
        }

        if (version >= 3)
        {
            for (uint j = 0; j < count1; j++)
                d[j].inner3data1 = reader.ReadStructureArray<uint>(reader.ReadInt32(), 4);
        }
        waypointData = d;

        uint mustBeFFFF = reader.ReadUInt32();
        if (mustBeFFFF != 0xffff)
            throw new InvalidDataException("Waypoint system end magic is not correct");
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(version);
        writer.Write(0);
        if (version >= 5)
            writer.Write(data, 0, 0x18);
        WaypointData[] d = waypointData;
        writer.Write(d.Length);
        for (int i = 0; i < d.Length; i++)
        {
            writer.Write(d[i].ii1);
            if (version >= 4)
                writer.Write(d[i].ii1ext);
            writer.Write(d[i].v1);

            writer.Write(d[i].innerdata1.Length);
            for (int j = 0; j < d[i].innerdata1.Length; j++)
                writer.Write(d[i].innerdata1[j]);

            writer.Write(d[i].innerdata2.Length);
            for (int j = 0; j < d[i].innerdata2.Length; j++)
                writer.Write(d[i].innerdata2[j]);
        }

        if (version >= 2)
        {
            WaypointInnerData[] d2 = inner2data1;
            writer.Write(d2.Length);
            for (int i = 0; i < d2.Length; i++)
            {
                writer.Write(d2[i].iiv2);
                writer.Write(d2[i].data.Length);
                for (int j = 0; j < d2[i].data.Length; j++)
                    writer.Write(d2[i].data[j]);
            }
        }

        if (version >= 3)
        {
            for (int i = 0; i < d.Length; i++)
            {
                var links = d[i].inner3data1 ?? [];
                writer.Write(links.Length);
                writer.WriteStructureArray(links, 4);
            }
        }

        writer.Write(0xffff);
    }
}
