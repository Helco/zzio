using System;
using System.IO;
using System.Numerics;
using zzio;

namespace zzio.scn;

[Serializable]
public class Misc : ISceneSection
{
    public string worldFile = "", worldPath = "", texturePath = "";
    public FColor ambientLight;
    public Vector3 v1, v2;
    public IColor clearColor;
    public byte fogType;
    public IColor fogColor;
    public float fogDistance;
    public float f1, farClip;

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
        using BinaryWriter writer = new(stream);
        writer.WriteZString(worldFile);
        writer.WriteZString(worldPath);
        writer.WriteZString(texturePath);
        ambientLight.Write(writer);
        writer.Write(v1);
        writer.Write(v2);
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
