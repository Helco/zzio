using System;
using System.Collections.Generic;
using System.IO;

namespace zzio.effect.parts
{
    [System.Serializable]
    public class ElectricBolt : IEffectPart
    {
        public EffectPartType Type { get { return EffectPartType.ElectricBolt; } }
        public string Name { get { return name; } }

        public uint
            phase1 = 1000,
            phase2 = 0,
            tileId = 1,
            tileW = 32,
            tileH = 32,
            tileCount = 1,
            color = 0xffffffff;
        public float
            branchDist = 1.0f,
            branchWidth = 1.0f;
        public string
            texName = "standard",
            name = "Eletric Bolt";

        public ElectricBolt() { }

        public void Read(BinaryReader r)
        {
            uint size = r.ReadUInt32();
            if (size != 108)
                throw new InvalidDataException("Invalid size of EffectPart ElectricBolt");

            phase1 = r.ReadUInt32();
            phase2 = r.ReadUInt32();
            branchDist = r.ReadSingle();
            branchWidth = r.ReadSingle();
            texName = Utils.readCAString(r, 32);
            tileId = r.ReadUInt32();
            tileW = r.ReadUInt32();
            tileH = r.ReadUInt32();
            tileCount = r.ReadUInt32();
            r.BaseStream.Seek(4, SeekOrigin.Current);
            color = r.ReadUInt32();
            name = Utils.readCAString(r, 32);
            r.BaseStream.Seek(4, SeekOrigin.Current);
        }
    }
}