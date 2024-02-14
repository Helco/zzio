using System.IO;

namespace zzio.effect.parts;

[System.Serializable]
public class RandomPlanes : IEffectPart
{
    public EffectPartType Type => EffectPartType.RandomPlanes;
    public string Name { get; set; } = "Random Planes";

    public uint
        phase1 = 1000,
        phase2 = 1000,
        spawnRate = 1,
        planeLife = 250,
        extraPhase,
        tileId,
        tileCount = 1,
        tileDuration = 50,
        tileW = 16,
        tileH = 16;
    public IColor color = new(255, 255, 255, 255);
    public uint amplColor;
    public float
        minProgress = 1.0f,
        amplPosX = 1.0f,
        amplPosY = 1.0f,
        rotationSpeedMult,
        texShift,
        scaleSpeedMult,
        targetSize,
        width = 0.1f,
        height = 0.1f,
        minScaleSpeed = 1.0f,
        maxScaleSpeed = 1.0f,
        yOffset,
        minPosX;
    public bool
        ignorePhases,
        circlesAround;
    public string texName = "standard";
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
        Name = r.ReadSizedCString(32);
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
