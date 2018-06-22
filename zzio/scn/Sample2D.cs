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
        public string filename;
        public UInt32 volume, // between 0-100
            loopCount;
        public byte c;

        public void read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            idx = reader.ReadUInt32();
            filename = reader.ReadZString();
            volume = reader.ReadUInt32();
            loopCount = reader.ReadUInt32();
            c = reader.ReadByte();
        }

        public void write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(idx);
            writer.WriteZString(filename);
            writer.Write(volume);
            writer.Write(loopCount);
            writer.Write(c);
        }
    }
}
