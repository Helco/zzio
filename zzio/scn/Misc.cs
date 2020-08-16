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
        public string worldFile = "", worldPath = "", texturePath = "";
        public FColor ambientLight;
        public Vector v1, v2;
        public IColor clearColor;
        public byte fogType;
        public IColor fogColor;
        public float fogDistance;
        public float f1, farClip;

        public void Read(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            worldFile = reader.ReadZString();
            worldPath = reader.ReadZString();
            texturePath = reader.ReadZString();
            ambientLight = FColor.ReadNew(reader);
            v1 = Vector.ReadNew(reader);
            v2 = Vector.ReadNew(reader);
            clearColor = IColor.ReadNew(reader);
            fogType = reader.ReadByte();
            if (fogType != 0)
            {
                fogColor = IColor.ReadNew(reader);
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

        public void Write(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.WriteZString(worldFile);
            writer.WriteZString(worldPath);
            writer.WriteZString(texturePath);
            ambientLight.Write(writer);
            v1.Write(writer);
            v2.Write(writer);
            clearColor.Write(writer);
            writer.Write(fogType);
            if (fogType != 0)
            {
                fogColor.Write(writer);
                writer.Write(fogDistance);
            }
            writer.Write(f1);
            writer.Write(farClip);
        }
    }
}
