using System;
using System.IO;
using System.Numerics;

namespace zzio.scn;

public enum FogType : byte
{
    None,
    Linear,
    Exponential,
    Exponential2
}

[Serializable]
public class Misc : ISceneSection
{
    public string worldFile = "", worldPath = "", texturePath = "";
    public FColor ambientLight;
    public Vector3 v1, v2;
    public IColor clearColor;
    public FogType fogType;
    public IColor fogColor;
    public float fogDistance, fogDensity;
    public float farClip;

    public void Read(Stream stream)
    {
        using BinaryReader reader = new(stream);
        worldFile = reader.ReadZString();
        worldPath = reader.ReadZString();
        texturePath = reader.ReadZString();
        ambientLight = FColor.ReadNew(reader);
        v1 = reader.ReadVector3();
        v2 = reader.ReadVector3();
        clearColor = IColor.ReadNew(reader);
        fogType = (FogType)reader.ReadByte();
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
        fogDensity = reader.ReadSingle();
        farClip = reader.ReadSingle();
    }

    public void Write(Stream stream)
    {
        using BinaryWriter writer = new(stream);
        writer.WriteZString(worldFile);
        writer.WriteZString(worldPath);
        writer.WriteZString(texturePath);
        ambientLight.Write(writer);
        writer.Write(v1);
        writer.Write(v2);
        clearColor.Write(writer);
        writer.Write((byte)fogType);
        if (fogType != 0)
        {
            fogColor.Write(writer);
            writer.Write(fogDistance);
        }
        writer.Write(fogDensity);
        writer.Write(farClip);
    }
}
