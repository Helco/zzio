using System;
using System.Text;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
    [System.Serializable]
    public class Sample2D : ISceneSection
    {
        public UInt32 idx;
        public string filename = "";
        public UInt32 volume, // between 0-100
            loopCount;
        public byte c;

        public void Read(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            idx = reader.ReadUInt32();
            filename = reader.ReadZString();
            volume = reader.ReadUInt32();
            loopCount = reader.ReadUInt32();
            c = reader.ReadByte();
        }

        public void Write(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(idx);
            writer.WriteZString(filename);
            writer.Write(volume);
            writer.Write(loopCount);
            writer.Write(c);
        }
    }
}
