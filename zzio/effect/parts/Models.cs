using System;
using System.Collections.Generic;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.effect.parts
{
    [System.Serializable]
    public class Models : IEffectPart
    {
        public EffectPartType Type { get { return EffectPartType.Models; } }
        public string Name { get { return name; } }

        public uint
            phase1 = 1000,
            phase2 = 1000,
            color = 0xffffffff;
        public float
            rotationSpeed = 0.0f,
            texShift = 0.0f,
            minProgress = 1.0f,
            minSize = 11.0f,
            fflag = 0.0f;
        public Vector
            rotationAxis = new Vector(),
            scaleSpeed = new Vector();
        public string
            modelName = "sphere",
            name = "Model";
        public bool
            ignoreHead = false,
            doTexShiftY = false;
        public EffectPartRenderMode renderMode = EffectPartRenderMode.AdditiveAlpha;

        public Models() { }

        public void Read(BinaryReader r)
        {
            uint size = r.ReadUInt32();
            if (size != 128 && size != 132)
                throw new InvalidDataException("Invalid size of EffectPart Models");

            phase1 = r.ReadUInt32();
            phase2 = r.ReadUInt32();
            rotationSpeed = r.ReadSingle();
            rotationAxis = Vector.read(r);
            scaleSpeed = Vector.read(r);
            texShift = r.ReadSingle();
            modelName = Utils.readCAString(r, 32);
            ignoreHead = r.ReadBoolean();
            color = r.ReadUInt32();
            name = Utils.readCAString(r, 32);
            r.BaseStream.Seek(3, SeekOrigin.Current);
            minProgress = r.ReadSingle();
            minSize = r.ReadSingle();
            r.BaseStream.Seek(4, SeekOrigin.Current);
            renderMode = EnumUtils.intToEnum<EffectPartRenderMode>(r.ReadInt32());
            if (size > 128)
                doTexShiftY = r.ReadSingle() == 0.0f; // don't look... it's legacy code behaviour
        }
    }
}