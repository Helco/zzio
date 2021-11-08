using System;
using System.IO;
using zzio.primitives;
using zzio.utils;

namespace zzio.scn
{
    [Serializable]
    public enum FOModelRenderType
    {
        EarlySolid = 0,
        LateAdditive = 1,
        EnvMap32 = 2,
        EnvMap64 = 3,
        EnvMap96 = 4, // Cathedral
        EnvMap128 = 5,
        EnvMap196 = 6, // London windows
        EnvMap255 = 7, // London cupboard windows
        Solid = 8, // Metallic?
        LateSolid = 9, // Plants?
        Additive = 10, // Cob webs 
        EarlyAdditive = 11,

        Unknown = -1
    }

    [Serializable]
    public class FOModel : ISceneSection
    {
        public uint idx;
        public string filename = "";
        public Vector pos, rot;
        public float fadeOutMin, fadeOutMax;
        public SurfaceProperties surfaceProps;
        public IColor color;
        public byte worldDetailLevel, unused;
        public FOModelRenderType renderType;
        public bool useCachedModels; // only acknoledged for last model
        public int wiggleSpeed;

        public void Read(Stream stream)
        {
            using BinaryReader reader = new BinaryReader(stream);
            idx = reader.ReadUInt32();
            filename = reader.ReadZString();
            pos = Vector.ReadNew(reader);
            rot = Vector.ReadNew(reader);
            fadeOutMin = reader.ReadSingle();
            fadeOutMax = reader.ReadSingle();
            surfaceProps = SurfaceProperties.ReadNew(reader);
            color = IColor.ReadNew(reader);
            worldDetailLevel = reader.ReadByte();
            unused = reader.ReadByte();
            renderType = EnumUtils.intToEnum<FOModelRenderType>(reader.ReadInt32());
            useCachedModels = reader.ReadBoolean();
            wiggleSpeed = reader.ReadInt32();
        }

        public void Write(Stream stream)
        {
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(idx);
            writer.WriteZString(filename);
            pos.Write(writer);
            rot.Write(writer);
            writer.Write(fadeOutMin);
            writer.Write(fadeOutMax);
            surfaceProps.Write(writer);
            color.Write(writer);
            writer.Write(worldDetailLevel);
            writer.Write(unused);
            writer.Write((int)renderType);
            writer.Write(useCachedModels);
            writer.Write(wiggleSpeed);
        }
    }
}
