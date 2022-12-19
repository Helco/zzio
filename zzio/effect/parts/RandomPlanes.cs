using System.IO;

namespace zzio.effect.parts
{
    [System.Serializable]
    public class RandomPlanes : IEffectPart
    {
        public EffectPartType Type => EffectPartType.RandomPlanes;
        public string Name => name;

        public uint
            phase1 = 1000,
            phase2 = 1000,
            spawnRate = 1,
            planeLife = 250,
            extraPhase = 0,
            tileId = 0,
            tileCount = 1,
            tileDuration = 50,
            tileW = 16,
            tileH = 16;
        public IColor color = new(255, 255, 255, 255);
        public uint amplColor = 0;
        public float
            minProgress = 1.0f,
            amplPosX = 1.0f,
            amplPosY = 1.0f,
            rotationSpeedMult = 0.0f,
            texShift = 0.0f,
            scaleSpeedMult = 0.0f,
            targetSize = 0.0f,
            width = 0.1f,
            height = 0.1f,
            minScaleSpeed = 1.0f,
            maxScaleSpeed = 1.0f,
            yOffset = 0.0f,
            minPosX = 0.0f;
        public bool
            ignorePhases = false,
            circlesAround = false;
        public string
            texName = "standard",
            name = "Random Planes";
        public EffectPartRenderMode renderMode = EffectPartRenderMode.AdditiveAlpha;

        public float Duration => (phase1 + phase2 + planeLife + extraPhase) / 1000f;

        public void Read(BinaryReader r)
        {
            uint size = r.ReadUInt32();
            if (size != 168 && size != 172 && size != 176)
                throw new InvalidDataException("Invalid size of EffectPart RandomPlanes");

            phase1 = r.ReadUInt32();
            phase2 = r.ReadUInt32();
            ignorePhases = r.ReadBoolean();
            r.BaseStream.Seek(3, SeekOrigin.Current);
            spawnRate = r.ReadUInt32();
            minProgress = r.ReadSingle();
            amplPosX = r.ReadSingle();
            amplPosY = r.ReadSingle();
            rotationSpeedMult = r.ReadSingle();
            texShift = r.ReadSingle();
            scaleSpeedMult = r.ReadSingle();
            targetSize = r.ReadSingle();
            width = r.ReadSingle();
            height = r.ReadSingle();
            planeLife = r.ReadUInt32();
            extraPhase = r.ReadUInt32();
            minScaleSpeed = r.ReadSingle();
            maxScaleSpeed = r.ReadSingle();
            texName = r.ReadSizedCString(32);
            tileId = r.ReadUInt32();
            tileCount = r.ReadUInt32();
            tileDuration = r.ReadUInt32();
            tileW = r.ReadUInt32();
            tileH = r.ReadUInt32();
            color = IColor.ReadNew(r);
            name = r.ReadSizedCString(32);
            amplColor = r.ReadUInt32();
            circlesAround = r.ReadBoolean();
            r.BaseStream.Seek(3, SeekOrigin.Current);
            yOffset = r.ReadSingle();
            if (size > 168)
                renderMode = EnumUtils.intToEnum<EffectPartRenderMode>(r.ReadInt32());
            if (size > 172)
                minPosX = r.ReadSingle();
        }
    }
}
