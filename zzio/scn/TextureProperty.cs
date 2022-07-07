using System;
using System.IO;

namespace zzio.scn
{
    [System.Serializable]
    public class TextureProperty : ISceneSection
    {
        public string fileName = "";
        public int footstepType;

        public void Read(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            fileName = reader.ReadZString();
            footstepType = reader.ReadInt32();
        }

        public void Write(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.WriteZString(fileName);
            writer.Write(footstepType);
        }
    }
}
