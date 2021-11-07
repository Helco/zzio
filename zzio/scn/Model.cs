using System;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
    [Serializable]
    public class Model : ISceneSection
    {
        public uint idx;
        public string filename = "";
        public Vector pos, rot;
        public SurfaceProperties surfaceProps;
        public IColor color;
        public bool useCachedModels; // ignored except for the last model in the scene...
        public int wiggleSpeed;
        public bool isVisualOnly; // if 0 has collision

        public void Read(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            idx = reader.ReadUInt32();
            filename = reader.ReadZString();
            pos = Vector.ReadNew(reader);
            rot = Vector.ReadNew(reader);
            surfaceProps = SurfaceProperties.ReadNew(reader);
            color = IColor.ReadNew(reader);
            useCachedModels = reader.ReadBoolean();
            wiggleSpeed = reader.ReadInt32();
            isVisualOnly = reader.ReadBoolean();
        }

        public void Write(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(idx);
            writer.WriteZString(filename);
            pos.Write(writer);
            rot.Write(writer);
            surfaceProps.Write(writer);
            color.Write(writer);
            writer.Write(useCachedModels);
            writer.Write(wiggleSpeed);
            writer.Write(isVisualOnly);
        }
    }
}
