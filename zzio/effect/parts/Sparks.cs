﻿using System.IO;

namespace zzio.effect.parts;

[System.Serializable]
public class Sparks : IEffectPart
{
    public EffectPartType Type => EffectPartType.Sparks;
    public string Name => name;

    public uint
        phase1 = 1000,
        phase2 = 1000,
        tileId = 0,
        tileW = 64,
        tileH = 64;
    public IColor color = new(255, 255, 255, 255);
    public uint spawnRate = 0;
    public float
        width = 0,
        height = 0,
        minSpawnProgress = 1.0f,
        startDistance = 0,
        speed = 0,
        maxDistance = 20.0f;
    public bool
        useSpeed = false,
        isSpawningMax = false;
    public string
        texName = "standard",
        name = "Sparks";

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
        name = r.ReadSizedCString(32);
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