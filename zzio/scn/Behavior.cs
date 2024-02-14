using System.IO;

namespace zzio.scn;

[System.Serializable]
public class Behavior : ISceneSection
{
    public BehaviourType type;
    public uint modelId;

    public void Read(Stream stream)
    {
        using BinaryReader reader = new(stream);
        type = EnumUtils.intToEnum<BehaviourType>(reader.ReadInt32());
        modelId = reader.ReadUInt32();
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write((int)type);
        writer.Write(modelId);
    }
}
