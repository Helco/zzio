using System;
using System.IO;

namespace zzio.scn
{
    [System.Serializable]
    public class Sample2D : ISceneSection
    {
        public uint idx;
        public string filename = "";
        public uint volume, // between 0-100
            loopCount;
        public byte c;

        public void Read(Stream stream)
        {
            using BinaryReader reader = new(stream);
            idx = reader.ReadUInt32();
            filename = reader.ReadZString();
            volume = reader.ReadUInt32();
            loopCount = reader.ReadUInt32();
            c = reader.ReadByte();
        }

        public void Write(Stream stream)
        {
            using BinaryWriter writer = new(stream);
            writer.Write(idx);
            writer.WriteZString(filename);
            writer.Write(volume);
            writer.Write(loopCount);
            writer.Write(c);
        }
    }
}
