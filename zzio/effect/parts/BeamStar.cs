using System;
using System.Collections.Generic;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.effect.parts
{
    [System.Serializable]
    public enum BeamStarComplexity
    {
        OnePlane = 0,
        TwoPlanes,
        FourPlanes,
    }

    [System.Serializable]
    public enum BeamStarMode
    {
        Constant = 0,
        Color,
        Shrink
    }

    [System.Serializable]
    public class BeamStar : IEffectPart
    {
        public EffectPartType Type => EffectPartType.BeamStar;
        public string Name => name;

        public uint
            phase1 = 1000,
            phase2 = 1000;
        public IColor color = new IColor(255, 255, 255, 255);
        public float
            width = 1.0f,
            scaleSpeedXY = 0.0f,
            startTexVEnd = 1.0f,
            rotationSpeed = 0.0f,
            texShiftVStart = 0.0f,
            endTexVEnd = 0.0f;
        public string
            texName = "standard",
            name = "Beam Star";
        public BeamStarComplexity complexity = BeamStarComplexity.OnePlane;
        public BeamStarMode mode = BeamStarMode.Constant;
        public EffectPartRenderMode renderMode = EffectPartRenderMode.AdditiveAlpha;

        public float Duration => (phase1 + phase2) / 1000f;

        public BeamStar() { }

        public void Read(BinaryReader r)
        {
            uint size = r.ReadUInt32();
            if (size != 128)
                throw new InvalidDataException("Invalid size of EffectPart BeamStar");

            phase1 = r.ReadUInt32();
            phase2 = r.ReadUInt32();
            complexity = EnumUtils.intToEnum<BeamStarComplexity>(r.ReadInt32());
            width = r.ReadSingle();
            scaleSpeedXY = r.ReadSingle();
            r.BaseStream.Seek(1, SeekOrigin.Current);
            texName = r.ReadSizedCString(32);
            r.BaseStream.Seek(3 + 2 * 4, SeekOrigin.Current);
            startTexVEnd = r.ReadSingle();
            rotationSpeed = r.ReadSingle();
            texShiftVStart = r.ReadSingle();
            r.BaseStream.Seek(1, SeekOrigin.Current);
            color = IColor.ReadNew(r);
            name = r.ReadSizedCString(32);
            r.BaseStream.Seek(3, SeekOrigin.Current);
            endTexVEnd = r.ReadSingle();
            mode = EnumUtils.intToEnum<BeamStarMode>(r.ReadInt32());
            renderMode = EnumUtils.intToEnum<EffectPartRenderMode>(r.ReadInt32());
        }
    }
}
