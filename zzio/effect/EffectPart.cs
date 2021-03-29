using System;

namespace zzio.effect
{
    [System.Serializable]
    public enum EffectPartType
    {
        MovingPlanes = 1,
        Models = 2,
        BeamStar = 3,
        ParticleEmitter = 4,
        RandomPlanes = 6,
        ParticleCollector = 7,
        Sparks = 8,
        Sound = 9,
        ParticleBeam = 10,
        ElectricBolt = 11,
        PlaneBeam = 12,

        Unknown = -1
    }

    [System.Serializable]
    public enum EffectPartRenderMode
    {
        AdditiveAlpha = 0,
        NormalBlend = 1,
        Additive = 2,

        Unknown = -1
    }

    public interface IEffectPart
    {
        EffectPartType Type { get; }
        string Name { get; }
        float Duration { get; }

        void Read(System.IO.BinaryReader reader);
    }
}
