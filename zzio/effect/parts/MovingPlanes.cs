using System.IO;

namespace zzio.effect.parts;

[System.Serializable]
public class MovingPlanes : IEffectPart
{
    public EffectPartType Type => EffectPartType.MovingPlanes;
    public string Name { get; set; } = "Moving Planes";

    public uint
        phase1 = 1000,
        phase2 = 1000,
        tileId,
        tileW = 64,
        tileH = 64;
    public IColor color = new(255, 255, 255, 255);
    public float
        width = 0.1f,
        height = 0.1f,
        sizeModSpeed,
        targetSize,
        rotation,
        texShift,
        minProgress = 1.0f,
        yOffset,
        xOffset;
    public string texName = "standard";
    public bool
        manualProgress,
        disableSecondPlane,
        circlesAround,
        useDirection;
    public EffectPartRenderMode renderMode = EffectPartRenderMode.AdditiveAlpha;

    public float Duration => (phase1 + phase2) / 1000f;

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
        Name = r.ReadSizedCString(32);
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
            xOffset = r.ReadSingle();
    }
}
