using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace zzio
{
    [System.Serializable]
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

    [System.Serializable]
    public struct MapMarker
    {
        public Int32 posX, posY;
        public MapMarkerSection section;
        public UInt32 sceneId;

        public static MapMarker[] read(byte[] buffer)
        {
            BinaryReader reader = new BinaryReader(new MemoryStream(buffer));
            UInt32 count = reader.ReadUInt32();
            MapMarker[] markers = new MapMarker[count];
            for (UInt32 i=0; i<count; i++)
            {
                MapMarker m;
                m.posX = reader.ReadInt32();
                m.posY = reader.ReadInt32();
                m.section = Utils.intToEnum<MapMarkerSection>(reader.ReadInt32());
                m.sceneId = reader.ReadUInt32();
                markers[i] = m;
            }
            return markers;
        }

        public static byte[] write(MapMarker[] markers)
        {
            MemoryStream stream = new MemoryStream();
            MapMarker.write(markers, stream);
            return stream.ToArray();
        }

        public static void write(MapMarker[] markers, Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(markers.Length);
            for (int i=0; i<markers.Length; i++)
            {
                writer.Write(markers[i].posX);
                writer.Write(markers[i].posY);
                writer.Write((int)markers[i].section);
                writer.Write(markers[i].sceneId);
            }
        }
    }
}
