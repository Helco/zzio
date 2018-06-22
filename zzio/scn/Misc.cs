using System;
using System.Text;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
    [Serializable]
    public class Misc : ISceneSection
    {
        public string worldFile, worldPath, texturePath;
        public FColor ambientLight;
        public Vector v1, v2;
        public IColor clearColor;
        public byte fogType;
        public IColor fogColor;
        public float fogDistance;
        public float f1, farClip;

        public void read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true);
            worldFile = reader.ReadZString();
            worldPath = reader.ReadZString();
            texturePath = reader.ReadZString();
            ambientLight = FColor.read(reader);
            v1 = Vector.read(reader);
            v2 = Vector.read(reader);
            clearColor = IColor.read(reader);
            fogType = reader.ReadByte();
            if (fogType != 0)
            {
                fogColor = IColor.read(reader);
                fogDistance = reader.ReadSingle();
            }
            else
            {
                fogColor = new IColor();
                fogDistance = 0.0f;
            }
            f1 = reader.ReadSingle();
            farClip = reader.ReadSingle();
        }

        public void write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.WriteZString(worldFile);
            writer.WriteZString(worldPath);
            writer.WriteZString(texturePath);
            ambientLight.write(writer);
            v1.write(writer);
            v2.write(writer);
            clearColor.write(writer);
            writer.Write(fogType);
            if (fogType != 0)
            {
                fogColor.write(writer);
                writer.Write(fogDistance);
            }
            writer.Write(f1);
            writer.Write(farClip);
        }
    }
}
