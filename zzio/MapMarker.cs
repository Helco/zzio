using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using zzio.utils;

namespace zzio
{
    public enum MapMarkerSection
    {
        FairyGarden = 1,
        EnchantedForest = 2,
        MountainWorld = 3,
        DarkSwamp = 4,
        ShadowRealm = 5,
        RealmOfClouds = 6,
        London = 7,

        Unknown = -1
    }

    [Serializable]
    public struct MapMarker
    {
        public Int32 posX, posY;
        public MapMarkerSection section;
        public UInt32 sceneId;

        public static MapMarker ReadNew(BinaryReader reader)
        {
            return new MapMarker
            {
                posX = reader.ReadInt32(),
                posY = reader.ReadInt32(),
                section = EnumUtils.intToEnum<MapMarkerSection>(reader.ReadInt32()),
                sceneId = reader.ReadUInt32()
            };
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(posX);
            writer.Write(posY);
            writer.Write((int)section);
            writer.Write(sceneId);
        }

        public static MapMarker[] ReadFile(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            int count = reader.ReadInt32();
            return Enumerable.Repeat(0, count)
                .Select(i => ReadNew(reader))
                .ToArray();
        }
        
        public static void WriteFile(MapMarker[] markers, Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(markers.Length);
            foreach (MapMarker mapMarker in markers)
                mapMarker.Write(writer);
        }
    }
}
