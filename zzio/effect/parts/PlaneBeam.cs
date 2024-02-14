﻿using System.IO;

namespace zzio.effect.parts;

[System.Serializable]
public class PlaneBeam : IEffectPart
{
    public EffectPartType Type => EffectPartType.PlaneBeam;
    public string Name { get; set; } = "Plane Beam";

    public uint
        phase1 = 1000,
        phase2 = 500,
        planeCount = 10,
        mode = 1, // TODO: Put this in an enum 
        tileId,
        tileW = 256,
        tileH = 256;
    public IColor color = new(255, 255, 255, 255);
    public uint mode2; // TODO: Also this 
    public float
        width = 1.0f,
        height = 1.0f,
        minScale,
        rotationMod,
        speed,
        widthScaleMod;
    public string texName = "standard";
    public EffectPartRenderMode renderMode = EffectPartRenderMode.AdditiveAlpha;

    public float Duration => (phase1 + phase2) / 1000f;

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
        color = IColor.ReadNew(r);
        Name = r.ReadSizedCString(32);
        renderMode = EnumUtils.intToEnum<EffectPartRenderMode>(r.ReadInt32());
        speed = r.ReadSingle();
        mode2 = r.ReadUInt32();
        widthScaleMod = r.ReadSingle();
    }
}