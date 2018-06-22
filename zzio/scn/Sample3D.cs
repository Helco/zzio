using System;
using System.Text;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
    [System.Serializable]
    public class Sample3D : ISceneSection
    {
        public UInt32 idx;
        public string filename;
        public Vector pos, forward, up;
        public float
            minDist,
            maxDist;
        public UInt32
            volume, // between 0-100
            loopCount,
            falloff;

        public void read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            idx = reader.ReadUInt32();
            filename = reader.ReadZString();
            pos = Vector.read(reader);
            forward = Vector.read(reader);
            up = Vector.read(reader);
            minDist = reader.ReadSingle();
            maxDist = reader.ReadSingle();
            volume = reader.ReadUInt32();
            loopCount = reader.ReadUInt32();
            falloff = reader.ReadUInt32();
        }

        public void write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(idx);
            writer.WriteZString(filename);
            pos.write(writer);
            forward.write(writer);
            up.write(writer);
            writer.Write(minDist);
            writer.Write(maxDist);
            writer.Write(volume);
            writer.Write(loopCount);
            writer.Write(falloff);
        }
    }
}
