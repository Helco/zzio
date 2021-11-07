using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.effect.parts
{
    [System.Serializable]
    public class Models : IEffectPart
    {
        public EffectPartType Type => EffectPartType.Models;
        public string Name => name;

        public uint
            phase1 = 1000,
            phase2 = 1000;
        public IColor color = new IColor(255, 255, 255, 255);
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

        public float Duration => (phase1 + phase2) / 1000f;

        public Models() { }

        public void Read(BinaryReader r)
        {
            uint size = r.ReadUInt32();
            if (size != 128 && size != 132)
                throw new InvalidDataException("Invalid size of EffectPart Models");

            phase1 = r.ReadUInt32();
            phase2 = r.ReadUInt32();
            rotationSpeed = r.ReadSingle();
            rotationAxis = Vector.ReadNew(r);
            scaleSpeed = Vector.ReadNew(r);
            texShift = r.ReadSingle();
            modelName = r.ReadSizedCString(32);
            ignoreHead = r.ReadBoolean();
            color = IColor.ReadNew(r);
            name = r.ReadSizedCString(32);
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