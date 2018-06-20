using System;
using System.Collections.Generic;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.effect.parts
{
    [System.Serializable]
    public enum ParticleType
    {
        Particle = 0,
        Spark,
        Model,
        
        Unknown = -1
    }

    [System.Serializable]
    public class ParticleEmitter : IEffectPart
    {
        public EffectPartType Type { get { return EffectPartType.ParticleEmitter; } }
        public string Name { get { return name; } }

        public uint
            head1 = 1000,
            head2 = 1000,
            minSpawnRateModProgress = 1,
            spawnRate = 1000,
            tileW = 256,
            tileH = 256,
            tileId = 0,
            tileCount = 1,
            tileDuration = 1,
            mode = 2; // TODO: Put this into an enum
        public float
            minVel = 1.0f,
            verticalDir = 90.0f,
            horRadius = 0.0f,
            verRadius = 0.0f;
        public ValueRangeAnimation
            life = new ValueRangeAnimation(1.0f, 0.0f),
            scale = new ValueRangeAnimation(0.2f, 0.0f, 2.0f),
            colorR = new ValueRangeAnimation(1.0f, 0.0f, 1.0f),
            colorG = new ValueRangeAnimation(1.0f, 0.0f, 1.0f),
            colorB = new ValueRangeAnimation(1.0f, 0.0f, 1.0f),
            colorA = new ValueRangeAnimation(1.0f, 0.0f, 1.0f),
            acc = new ValueRangeAnimation(1.0f, 0.0f);
        public Vector
            gravity = new Vector(0.0f, 0.0f, 0.0f),
            gravityMod = new Vector(0.0f, 0.0f, 0.0f);
        public bool
            hasDirection = false;
        public string
            name = "Particle Emitter",
            texName = "standard";
        public ParticleType type = ParticleType.Particle;
        public EffectPartRenderMode renderMode = EffectPartRenderMode.AdditiveAlpha;

        public ParticleEmitter() { }

        public void Read(BinaryReader r)
        {
            uint size = r.ReadUInt32();
            if (size != 288 && size != 292)
                throw new InvalidDataException("Invalid size of EffectPart ParticleEmitter");

            head1 = r.ReadUInt32();
            head2 = r.ReadUInt32();
            r.BaseStream.Seek(4 * 4 + 32 + 1, SeekOrigin.Current); // many unused variables
            name = r.ReadSizedCString(32);
            r.BaseStream.Seek(3, SeekOrigin.Current);
            minSpawnRateModProgress = r.ReadUInt32();
            life.value = r.ReadSingle();
            spawnRate = r.ReadUInt32();
            minVel = r.ReadSingle();
            verticalDir = r.ReadSingle();
            scale.value = r.ReadSingle();
            scale.mod = r.ReadSingle();
            colorR.value = r.ReadSingle();
            colorR.mod = r.ReadSingle();
            colorG.value = r.ReadSingle();
            colorG.mod = r.ReadSingle();
            colorB.value = r.ReadSingle();
            colorB.mod = r.ReadSingle();
            colorA.value = r.ReadSingle();
            colorA.mod = r.ReadSingle();
            colorA.width = r.ReadSingle();
            colorR.width = r.ReadSingle();
            colorG.width = r.ReadSingle();
            colorB.width = r.ReadSingle();
            life.width = r.ReadSingle();
            acc.width = r.ReadSingle();
            scale.width = r.ReadSingle();
            gravity = Vector.read(r);
            gravityMod = Vector.read(r);
            type = EnumUtils.intToEnum<ParticleType>(r.ReadInt32());
            texName = r.ReadSizedCString(32);
            tileW = r.ReadUInt32();
            tileH = r.ReadUInt32();
            tileId = r.ReadUInt32();
            tileCount = r.ReadUInt32();
            tileDuration = r.ReadUInt32();
            acc.value = r.ReadSingle();
            r.BaseStream.Seek(4, SeekOrigin.Current);
            horRadius = r.ReadSingle();
            verRadius = r.ReadSingle();
            mode = r.ReadUInt32();
            r.BaseStream.Seek(4, SeekOrigin.Current);
            hasDirection = r.ReadBoolean();
            r.BaseStream.Seek(3, SeekOrigin.Current);
            if (size > 288)
                renderMode = EnumUtils.intToEnum<EffectPartRenderMode>(r.ReadInt32());
        }
    }
}