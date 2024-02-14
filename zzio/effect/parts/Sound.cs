using System.IO;

namespace zzio.effect.parts;

[System.Serializable]
public class Sound : IEffectPart
{
    public EffectPartType Type => EffectPartType.Sound;
    public string Name => name;

    public uint
        volume = 100;
    public float
        minDist,
        maxDist;
    public bool
        isDisabled;
    public string
        fileName = "standard",
        name = "Sound Effect";

    public float Duration => 0f;

    public void Read(BinaryReader r)
    {
        uint size = r.ReadUInt32();
        if (size != 84)
            throw new InvalidDataException("Invalid size of EffectPart Sound");

        volume = r.ReadUInt32();
        minDist = r.ReadSingle();
        maxDist = r.ReadSingle();
        r.BaseStream.Seek(4, SeekOrigin.Current);
        isDisabled = r.ReadBoolean();
        fileName = r.ReadSizedCString(32);
        name = r.ReadSizedCString(32);
        r.BaseStream.Seek(3, SeekOrigin.Current);
    }
}
