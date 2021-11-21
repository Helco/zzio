using System;
using System.IO;

namespace zzio.scn
{
    [Serializable]
    public class SceneItem : ISceneSection
    {
        public string name = "";
        public UInt32 index, type;

        public void Read(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            name = reader.ReadZString();
            index = reader.ReadUInt32();
            type = reader.ReadUInt32();
        }

        public void Write(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.WriteZString(name);
            writer.Write(index);
            writer.Write(type);
        }
    }
}
