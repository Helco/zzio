using System;
using System.Text;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
    [System.Serializable]
    public class Behavior : ISceneSection
    {
        public BehaviourType type;
        public UInt32 modelId;

        public void Read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            type = EnumUtils.intToEnum<BehaviourType>(reader.ReadInt32());
            modelId = reader.ReadUInt32();
        }

        public void Write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write((int)type);
            writer.Write(modelId);
        }
    }

}
