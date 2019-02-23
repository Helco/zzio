using System;
using System.Collections.Generic;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.effect.parts
{
    [System.Serializable]
    public class MovingPlanes : IEffectPart
    {
        public EffectPartType Type => EffectPartType.MovingPlanes;
        public string Name => name;

        public uint
            phase1 = 1000,
            phase2 = 1000,
            tileId = 0,
            tileW = 64,
            tileH = 64;
        public IColor color = new IColor(255, 255, 255, 255);
        public float
            width = 0.1f,
            height = 0.1f,
            sizeModSpeed = 0.0f,
            targetSize = 0.0f,
            rotation = 0.0f,
            texShift = 0.0f,
            minProgress = 1.0f,
            yOffset = 0.0f;
        public string
            texName = "standard",
            name = "Moving Planes";
        public bool
            manualProgress = false,
            disableSecondPlane = false,
            circlesAround = false,
            useDirection = false;
        public EffectPartRenderMode renderMode = EffectPartRenderMode.AdditiveAlpha;

        public MovingPlanes() { }

        public void Read(BinaryReader r)
        {
            uint size = r.ReadUInt32();
            if (size != 136 && size != 140)
                throw new InvalidDataException("Invalid size of EffectPart MovingPlanes");

            phase1 = r.ReadUInt32();
            phase2 = r.ReadUInt32();
            width = r.ReadSingle();
            height = r.ReadSingle();
            sizeModSpeed = r.ReadSingle();
            targetSize = r.ReadSingle();
            rotation = r.ReadSingle();
            texShift = r.ReadSingle();
            texName = r.ReadSizedCString(32);
            tileId = r.ReadUInt32();
            tileW = r.ReadUInt32();
            tileH = r.ReadUInt32();
            manualProgress = r.ReadBoolean();
            color = IColor.ReadNew(r);
            name = r.ReadSizedCString(32);
            r.BaseStream.Seek(3, SeekOrigin.Current);
            minProgress = r.ReadSingle();
            disableSecondPlane = r.ReadBoolean();
            circlesAround = r.ReadBoolean();
            r.BaseStream.Seek(2, SeekOrigin.Current);
            yOffset = r.ReadSingle();
            renderMode = EnumUtils.intToEnum<EffectPartRenderMode>(r.ReadInt32());
            useDirection = r.ReadBoolean();
            r.BaseStream.Seek(3, SeekOrigin.Current);
            if (size > 136)
                r.BaseStream.Seek(4, SeekOrigin.Current); // unused value            
        }
    }
}
