using System;
using System.IO;
using System.Numerics;
using zzio;

namespace zzio.scn;

[Serializable]
public class Model : ISceneSection
{
    public uint idx;
    public string filename = "";
    public Vector3 pos, rot;
    public SurfaceProperties surfaceProps;
    public IColor color;
    public bool useCachedModels; // ignored except for the last model in the scene...
    public int wiggleAmpl;
    public bool isVisualOnly; // if 0 has collision

    public void Read(Stream stream)
    {
        using BinaryReader reader = new(stream);
        idx = reader.ReadUInt32();
        filename = reader.ReadZString();
        pos = reader.ReadVector3();
        rot = reader.ReadVector3();
        surfaceProps = SurfaceProperties.ReadNew(reader);
        color = IColor.ReadNew(reader);
        useCachedModels = reader.ReadBoolean();
        wiggleAmpl = reader.ReadInt32();
        isVisualOnly = reader.ReadBoolean();
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.Write(idx);
        writer.WriteZString(filename);
        writer.Write(pos);
        writer.Write(rot);
        surfaceProps.Write(writer);
        color.Write(writer);
        writer.Write(useCachedModels);
        writer.Write(wiggleAmpl);
        writer.Write(isVisualOnly);
    }
}
