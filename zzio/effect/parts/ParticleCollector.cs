using System;
using System.Collections.Generic;
using System.IO;

namespace zzio.effect.parts
{
    [System.Serializable]
    public class ParticleCollector : IEffectPart
    {
        public EffectPartType Type { get { return EffectPartType.ParticleCollector; } }
        public string Name { get { return name; } }

        public uint
            maxCount = 0,
            tileW = 256,
            tileH = 256,
            tileDuration = 1,
            tileCount = 1,
            tileId = 0,
            color = 0xffffffff,
            mode = 0; // TODO: Into an enum with this!
        public float
            speed = 1.0f,
            radius = 1.0f,
            parWidth = 0.05f,
            parHeight = 0.05f,
            minProgress = 1.0f;
        public string
            texName = "standard",
            name = "Particle Collector";

        public ParticleCollector() { }

        public void Read(BinaryReader r)
        {
            uint size = r.ReadUInt32();
            if (size != 120)
                throw new InvalidDataException("Invalid size of EffectPart ParticleCollector");

            maxCount = r.ReadUInt32();
            speed = r.ReadSingle();
            radius = r.ReadSingle();
            parWidth = r.ReadSingle();
            parHeight = r.ReadSingle();
            texName = Utils.readCAString(r, 32);
            tileW = r.ReadUInt32();
            tileH = r.ReadUInt32();
            tileDuration = r.ReadUInt32();
            tileCount = r.ReadUInt32();
            tileId = r.ReadUInt32();
            color = r.ReadUInt32();
            name = Utils.readCAString(r, 32);
            mode = r.ReadUInt32();
            minProgress = r.ReadSingle();
            r.BaseStream.Seek(4, SeekOrigin.Current);
        }
    }
}
