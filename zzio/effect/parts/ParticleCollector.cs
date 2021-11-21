using System.IO;

namespace zzio.effect.parts
{
    public enum ParticleCollectorMode
    {
        Tornado = 0,
        Cycle = 1,
        Standard = 2,

        Unknown = -1
    }

    [System.Serializable]
    public class ParticleCollector : IEffectPart
    {
        public EffectPartType Type => EffectPartType.ParticleCollector;
        public string Name => name;

        public uint
            maxCount = 0,
            tileW = 256,
            tileH = 256,
            tileDuration = 1,
            tileCount = 1,
            tileId = 0;
        public IColor color = new IColor(255, 255, 255, 255);
        public ParticleCollectorMode mode = ParticleCollectorMode.Tornado;
        public float
            speed = 1.0f,
            radius = 1.0f,
            parWidth = 0.05f,
            parHeight = 0.05f,
            minProgress = 1.0f;
        public string
            texName = "standard",
            name = "Particle Collector";

        public float Duration => 1f;

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
            texName = r.ReadSizedCString(32);
            tileW = r.ReadUInt32();
            tileH = r.ReadUInt32();
            tileDuration = r.ReadUInt32();
            tileCount = r.ReadUInt32();
            tileId = r.ReadUInt32();
            color = IColor.ReadNew(r);
            name = r.ReadSizedCString(32);
            mode = EnumUtils.intToEnum<ParticleCollectorMode>(r.ReadInt32());
            minProgress = r.ReadSingle();
            r.BaseStream.Seek(4, SeekOrigin.Current);
        }
    }
}
