using System;
using System.IO;
using System.Numerics;
using System.Collections.Generic;

// This surely needs some reverse engineering work being done...
namespace zzio.scn;

[Serializable]
public struct Waypoint
{
    public uint Id, Group;
    public Vector3 Position;
    public uint[] WalkableIds, JumpableIds;
    public uint[]? VisibleIds;
}

[Serializable]
public class WaypointSystem : ISceneSection
{
    public uint Version;
    public byte[] Data = new byte[0x18]; // most likely just for generation? AFAIK not used in game
    public Waypoint[] Waypoints = [];
    public Dictionary<uint, uint[]> CompatibleGroups = [];

    public void Read(Stream stream)
    {
        using BinaryReader reader = new(stream);
        Version = reader.ReadUInt32();
        uint mustBeZero = reader.ReadUInt32();
        if (mustBeZero != 0)
            throw new InvalidDataException("Waypoint system start magic is not correct");

        if (Version >= 5)
            Data = reader.ReadBytes(0x18);
        uint count1 = reader.ReadUInt32();
        Waypoint[] d = new Waypoint[count1];
        for (uint i = 0; i < count1; i++)
        {
            d[i].Id = reader.ReadUInt32();
            if (Version >= 4)
                d[i].Group = reader.ReadUInt32();
            d[i].Position = reader.ReadVector3();

            d[i].WalkableIds = reader.ReadStructureArray<uint>(reader.ReadInt32(), 4);
            d[i].JumpableIds = reader.ReadStructureArray<uint>(reader.ReadInt32(), 4);
        }

        if (Version >= 2)
        {
            int groupCount = reader.ReadInt32();
            CompatibleGroups = new(groupCount);
            for (int j = 0; j < groupCount; j++)
            {
                CompatibleGroups.Add(
                    reader.ReadUInt32(),
                    reader.ReadStructureArray<uint>(reader.ReadInt32(), 4));
            }
        }

        if (Version >= 3)
        {
            for (uint j = 0; j < count1; j++)
                d[j].VisibleIds = reader.ReadStructureArray<uint>(reader.ReadInt32(), 4);
        }
        Waypoints = d;

        uint mustBeFFFF = reader.ReadUInt32();
        if (mustBeFFFF != 0xffff)
            throw new InvalidDataException("Waypoint system end magic is not correct");
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(Version);
        writer.Write(0);
        if (Version >= 5)
            writer.Write(Data, 0, 0x18);
        Waypoint[] d = Waypoints;
        writer.Write(d.Length);
        for (int i = 0; i < d.Length; i++)
        {
            writer.Write(d[i].Id);
            if (Version >= 4)
                writer.Write(d[i].Group);
            writer.Write(d[i].Position);

            writer.Write(d[i].WalkableIds.Length);
            for (int j = 0; j < d[i].WalkableIds.Length; j++)
                writer.Write(d[i].WalkableIds[j]);

            writer.Write(d[i].JumpableIds.Length);
            for (int j = 0; j < d[i].JumpableIds.Length; j++)
                writer.Write(d[i].JumpableIds[j]);
        }

        if (Version >= 2)
        {
            writer.Write(CompatibleGroups.Count);
            foreach (var (groupId, wpIds) in CompatibleGroups)
            {
                writer.Write(groupId);
                writer.WriteStructureArray(wpIds, 4);
            }
        }

        if (Version >= 3)
        {
            for (int i = 0; i < d.Length; i++)
            {
                var links = d[i].VisibleIds ?? [];
                writer.Write(links.Length);
                writer.WriteStructureArray(links, 4);
            }
        }

        writer.Write(0xffff);
    }
}
