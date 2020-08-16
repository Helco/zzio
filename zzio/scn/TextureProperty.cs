using System;
using System.Text;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
    [System.Serializable]
    public class TextureProperty : ISceneSection
    {
        public string fileName = "";
        public Int32 footstepType;

        public void Read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            fileName = reader.ReadZString();
            footstepType = reader.ReadInt32();
        }

        public void Write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.WriteZString(fileName);
            writer.Write(footstepType);
        }
    }
}
