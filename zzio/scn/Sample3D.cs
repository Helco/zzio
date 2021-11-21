using System;
using System.IO;
using System.Numerics;
using zzio;

namespace zzio.scn
{
    [System.Serializable]
    public class Sample3D : ISceneSection
    {
        public UInt32 idx;
        public string filename = "";
        public Vector3 pos, forward, up;
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
            pos = reader.ReadVector3();
            forward = reader.ReadVector3();
            up = reader.ReadVector3();
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
            writer.Write(pos);
            writer.Write(forward);
            writer.Write(up);
            writer.Write(minDist);
            writer.Write(maxDist);
            writer.Write(volume);
            writer.Write(loopCount);
            writer.Write(falloff);
        }
    }
}
