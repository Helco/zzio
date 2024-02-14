﻿using System.IO;

namespace zzio.effect.parts;

[System.Serializable]
public class Sparks : IEffectPart
{
    public EffectPartType Type => EffectPartType.Sparks;
    public string Name { get; set; } = nameof(Sparks);

    public uint
        phase1 = 1000,
        phase2 = 1000,
        tileId,
        tileW = 64,
        tileH = 64;
    public IColor color = new(255, 255, 255, 255);
    public uint spawnRate;
    public float
        width,
        height,
        minSpawnProgress = 1.0f,
        startDistance,
        speed,
        maxDistance = 20.0f;
    public bool
        useSpeed,
        isSpawningMax;
    public string texName = "standard";

    public float Duration => (phase1 + phase2) / 1000f;

    public void Read(BinaryReader r)
    {
        uint size = r.ReadUInt32();
        if (size != 128 && size != 132)
            throw new InvalidDataException("Invalid size of EffectPart Sparks");

        phase1 = r.ReadUInt32();
        phase2 = r.ReadUInt32();
        width = r.ReadSingle();
        height = r.ReadSingle();
        texName = r.ReadSizedCString(32);
        tileId = r.ReadUInt32();
        tileW = r.ReadUInt32();
        tileH = r.ReadUInt32();
        r.BaseStream.Seek(1, SeekOrigin.Current);
        color = IColor.ReadNew(r);
        Name = r.ReadSizedCString(32);
        r.BaseStream.Seek(3, SeekOrigin.Current);
        minSpawnProgress = r.ReadSingle();
        startDistance = r.ReadSingle();
        speed = r.ReadSingle();
        spawnRate = r.ReadUInt32();
        useSpeed = r.ReadUInt32() != 0;
        maxDistance = r.ReadSingle();
        isSpawningMax = r.ReadUInt32() != 0;
        if (size > 128)
            r.BaseStream.Seek(4, SeekOrigin.Current);
    }
}