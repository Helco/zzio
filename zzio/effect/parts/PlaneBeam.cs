using System;
using System.Collections.Generic;
using System.IO;
using zzio.utils;

namespace zzio.effect.parts
{
    [System.Serializable]
    public class PlaneBeam : IEffectPart
    {
        public EffectPartType Type => EffectPartType.PlaneBeam;
        public string Name => name;

        public uint
            phase1 = 1000,
            phase2 = 500,
            planeCount = 10,
            mode = 1, // TODO: Put this in an enum
            tileId = 0,
            tileW = 256,
            tileH = 256,
            color = 0xffffffff,
            mode2 = 0; // TODO: Also this
        public float
            width = 1.0f,
            height = 1.0f,
            minScale = 0.0f,
            rotationMod = 0.0f,
            speed = 0.0f,
            widthScaleMod = 0.0f;
        public string
            texName = "standard",
            name = "PlaneBeam";
        public EffectPartRenderMode renderMode = EffectPartRenderMode.AdditiveAlpha;

        public PlaneBeam() { }

        public void Read(BinaryReader r)
        {
            uint size = r.ReadUInt32();
            if (size != 132)
                throw new InvalidDataException("Invalid size of EffectPart PlaneBeam");

            phase1 = r.ReadUInt32();
            phase2 = r.ReadUInt32();
            r.BaseStream.Seek(4, SeekOrigin.Current);
            planeCount = r.ReadUInt32();
            mode = r.ReadUInt32();
            width = r.ReadSingle();
            height = r.ReadSingle();
            minScale = r.ReadSingle();
            rotationMod = r.ReadSingle();
            texName = r.ReadSizedCString(32);
            tileId = r.ReadUInt32();
            tileW = r.ReadUInt32();
            tileH = r.ReadUInt32();
            color = r.ReadUInt32();
            name = r.ReadSizedCString(32);
            renderMode = EnumUtils.intToEnum<EffectPartRenderMode>(r.ReadInt32());
            speed = r.ReadSingle();
            mode2 = r.ReadUInt32();
            widthScaleMod = r.ReadSingle();
        }
    }
}