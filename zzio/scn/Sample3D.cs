using System;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
    [System.Serializable]
    public class Sample3D : ISceneSection
    {
        public UInt32 idx;
        public string filename = "";
        public Vector pos, forward, up;
        public float
            minDist,
            maxDist;
        public UInt32
            volume, // between 0-100
            loopCount,
            falloff;

        public void Read(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            idx = reader.ReadUInt32();
            filename = reader.ReadZString();
            pos = Vector.ReadNew(reader);
            forward = Vector.ReadNew(reader);
            up = Vector.ReadNew(reader);
            minDist = reader.ReadSingle();
            maxDist = reader.ReadSingle();
            volume = reader.ReadUInt32();
            loopCount = reader.ReadUInt32();
            falloff = reader.ReadUInt32();
        }

        public void Write(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(idx);
            writer.WriteZString(filename);
            pos.Write(writer);
            forward.Write(writer);
            up.Write(writer);
            writer.Write(minDist);
            writer.Write(maxDist);
            writer.Write(volume);
            writer.Write(loopCount);
            writer.Write(falloff);
        }
    }
}
