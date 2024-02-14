using System.IO;

namespace zzio.effect.parts;

[System.Serializable]
public class ElectricBolt : IEffectPart
{
    public EffectPartType Type => EffectPartType.ElectricBolt;
    public string Name { get; set; } = "Electric Bolt";

    public uint
        phase1 = 1000,
        phase2,
        tileId = 1,
        tileW = 32,
        tileH = 32,
        tileCount = 1;
    public IColor color = new(255, 255, 255, 255);
    public float
        branchDist = 1.0f,
        branchWidth = 1.0f;
    public string texName = "standard";

    public float Duration => (phase1 + phase2) / 1000f;

    public void Read(BinaryReader r)
    {
        uint size = r.ReadUInt32();
        if (size != 108)
            throw new InvalidDataException("Invalid size of EffectPart ElectricBolt");

        phase1 = r.ReadUInt32();
        phase2 = r.ReadUInt32();
        branchDist = r.ReadSingle();
        branchWidth = r.ReadSingle();
        texName = r.ReadSizedCString(32);
        tileId = r.ReadUInt32();
        tileW = r.ReadUInt32();
        tileH = r.ReadUInt32();
        tileCount = r.ReadUInt32();
        r.BaseStream.Seek(4, SeekOrigin.Current);
        color = IColor.ReadNew(r);
        Name = r.ReadSizedCString(32);
        r.BaseStream.Seek(4, SeekOrigin.Current);
    }
}