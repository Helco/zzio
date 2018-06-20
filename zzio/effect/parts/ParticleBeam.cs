using System;
using System.Collections.Generic;
using System.IO;
using zzio.utils;

namespace zzio.effect.parts
{
    [System.Serializable]
    public enum ParticleBeamMode // TODO: Check if this actually applies...
    {
        Tornado = 0,
        Cycle,
        Standard
    }

    [System.Serializable]
    public class ParticleBeam : IEffectPart
    {
        public EffectPartType Type { get { return EffectPartType.ParticleBeam; } }
        public string Name { get { return name; } }

        public uint
            phase1 = 1000,
            phase2 = 1000,
            maxCount = 100,
            tileId = 0,
            tileCount = 1,
            tileDuration = 1,
            tileW = 32,
            tileH = 32,
            color = 0xffffffff,
            fadeMode = 0; // TODO: Put this in an enum
        public float
            parWidth = 0.1f,
            parHeight = 0.1f,
            beamWidth = 0.0f,
            beamHeight = 0.0f,
            zSpeed = 0.0f,
            fadeXSpeed = 0.0f,
            fadeYSpeed = 0.0f,
            fadeSpeed = 0.0f;
        public bool
            isEqualFade = false;
        public string
            texName = "standard",
            name = "Particle Beam";
        public ParticleBeamMode mode = ParticleBeamMode.Tornado;
        public EffectPartRenderMode renderMode = EffectPartRenderMode.AdditiveAlpha;

        public ParticleBeam() { }

        public void Read(BinaryReader r)
        {
            uint size = r.ReadUInt32();
            if (size != 156)
                throw new InvalidDataException("Invalid size of EffectPart ParticleBeam");

            phase1 = r.ReadUInt32();
            phase2 = r.ReadUInt32();
            maxCount = r.ReadUInt32();
            isEqualFade = r.ReadBoolean();
            r.BaseStream.Seek(3 + 4, SeekOrigin.Current);
            parWidth = r.ReadSingle();
            parHeight = r.ReadSingle();
            r.BaseStream.Seek(4, SeekOrigin.Current);
            texName = r.ReadSizedCString(32);
            tileId = r.ReadUInt32();
            tileCount = r.ReadUInt32();
            tileDuration = r.ReadUInt32();
            tileW = r.ReadUInt32();
            tileH = r.ReadUInt32();
            color = r.ReadUInt32();
            name = r.ReadSizedCString(32);
            mode = EnumUtils.intToEnum<ParticleBeamMode>(r.ReadInt32());
            fadeMode = r.ReadUInt32();
            beamWidth = r.ReadSingle();
            beamHeight = r.ReadSingle();
            zSpeed = r.ReadSingle();
            fadeXSpeed = r.ReadSingle();
            fadeYSpeed = r.ReadSingle();
            fadeSpeed = r.ReadSingle();
            renderMode = EnumUtils.intToEnum<EffectPartRenderMode>(r.ReadInt32());
        }
    }
}