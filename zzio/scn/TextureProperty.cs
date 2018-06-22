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
        public string fileName;
        public Int32 footstepType;

        public void read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            fileName = reader.ReadZString();
            footstepType = reader.ReadInt32();
        }

        public void write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.WriteZString(fileName);
            writer.Write(footstepType);
        }
    }
}
